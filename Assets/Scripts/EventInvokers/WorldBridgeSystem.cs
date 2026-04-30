using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq.Expressions;
using FracturedStudios.Data;
using System.Threading;
using System.Threading.Tasks;
using FracturedStudios.Utils;

namespace FracturedStudios.Invoker
{
    [DefaultExecutionOrder(-139)]
    // Merged WorldBridgeSystem: includes expression-based invoker helpers and routing utilities.
    public class WorldBridgeSystem : MonoBehaviour
    {
        public static WorldBridgeSystem? Instance { get; private set; }

        // Maps stable IDs -> UnityEngine.Object (GameObject, Component, etc.)
        private readonly ConcurrentDictionary<string, UnityEngine.Object> _idRegistry = new();

        public PlayerData data;
        // Cache for reflection results to improve performance
        private readonly Dictionary<(string id, string member), MemberInfo> _memberCache = new();
        private readonly Dictionary<(string id, string method), MethodInfo> _methodCache = new();
        // Cached boxed getters/setters to avoid reflection on hot paths.
        private readonly ConcurrentDictionary<(Type, string), Func<object, object>> _boxedGetterCache = new();
        private readonly ConcurrentDictionary<(Type, string), Action<object, object>> _boxedSetterCache = new();

        private DynamicDictionaryInvoker _invoker;
        public event Action<string, UnityEngine.Object> OnIdRegistered;
        public event Action<string> OnIdUnregistered;
        // Cancellation token source that is cancelled when the WorldBridgeSystem is destroyed
        private CancellationTokenSource _shutdownCts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _invoker = FindObjectOfType<DynamicDictionaryInvoker>();
            if (_invoker == null)
            {
                var go = new GameObject("DynamicDictionaryInvoker");
                DontDestroyOnLoad(go);
                _invoker = go.AddComponent<DynamicDictionaryInvoker>();
                #if UNITY_EDITOR
                Debug.Log($"[{nameof(WorldBridgeSystem)}] Created {nameof(DynamicDictionaryInvoker)}");
                #endif
            }
            // create shutdown CTS after invoker setup
            _shutdownCts = new CancellationTokenSource();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                _idRegistry.Clear();
                _memberCache.Clear();
                _methodCache.Clear();
                // cancel any registered shutdown-aware operations
                try { _shutdownCts?.Cancel(); } catch { }
                try { _shutdownCts?.Dispose(); } catch { }
                Instance = null;
            }
        }

        /// <summary>
        /// Cancellation token that is cancelled when the WorldBridgeSystem is destroyed.
        /// Use this to tie background work to the application's lifetime.
        /// </summary>
        public CancellationToken GetShutdownToken()
        {
            return _shutdownCts?.Token ?? CancellationToken.None;
        }

        // Expression-based invoker builder (returns object or null for void)
        public static Func<object, object[], object> BuildInvokerReturn(MethodInfo mi)
        {
            return CreateInvoker(mi);
        }

        public static Func<object, object[], object> CreateInvoker(MethodInfo mi)
        {
            var instance = Expression.Parameter(typeof(object), "instance");
            var args = Expression.Parameter(typeof(object[]), "args");

            var parameters = mi.GetParameters();
            var paramExprs = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var idx = Expression.Constant(i);
                var accessor = Expression.ArrayIndex(args, idx);
                var convert = Expression.Convert(accessor, parameters[i].ParameterType);
                paramExprs[i] = convert;
            }

            var instanceCast = Expression.Convert(instance, mi.DeclaringType);
            var call = Expression.Call(instanceCast, mi, paramExprs);
            Expression body = mi.ReturnType == typeof(void) ?
                (Expression)Expression.Block(call, Expression.Constant(null)) :
                Expression.Convert(call, typeof(object));

            var lambda = Expression.Lambda<Func<object, object[], object>>(body, instance, args);
            return lambda.Compile();
        }

        public interface IWorldBridgeRegistrable
        {
            string BridgeId { get; }
            string BridgeGroup { get; } // optional grouping (e.g., "ui","npc","world")
            object BridgeMetadata { get; } // small DTO for discovery
            void OnRegistered(WorldBridgeSystem bridge);
            void OnUnregistered(WorldBridgeSystem bridge);
        }

        #region Registry
        public void RegisterID(string id, UnityEngine.Object target)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }
            if (target == null)
            {
                return;
            }

            id = string.Intern(id);
            _idRegistry[id] = target;
            try { OnIdRegistered?.Invoke(id, target); } catch { }
            #if UNITY_EDITOR
            Debug.Log($"[{nameof(WorldBridgeSystem)}] Registered ID {id} -> {target.name} ({target.GetType().Name})");
            #endif
        }

        public void UnregisterID(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }
            if (_idRegistry.TryRemove(id, out _))
            {
                try { OnIdUnregistered?.Invoke(id); } catch { }
                #if UNITY_EDITOR
                Debug.Log($"[{nameof(WorldBridgeSystem)}] Unregistered ID {id}");
                #endif

                // Clean up any invoker registrations that used this id as their entry id
                try { _invoker?.RemoveAllEntriesForId(id); } catch { }
            }
        }

        public T GetByID<T>(string id) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            if (_idRegistry.TryGetValue(id, out var obj))
            {
                if (obj is T typedObj) return typedObj;
                return null;
            }
            return null;
        }

        /// <summary>
        /// Await a registered ID without frame polling.
        /// </summary>
        public async Task<T> AwaitRegisteredIDAsync<T>(string id, int timeoutMs = -1, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            id = string.Intern(id);

            if (_idRegistry.TryGetValue(id, out var existing))
                return existing as T;

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            void HandleRegistered(string registeredId, UnityEngine.Object target)
            {
                if (!string.Equals(registeredId, id, StringComparison.Ordinal)) return;
                tcs.TrySetResult(target as T);
            }

            OnIdRegistered += HandleRegistered;

            try
            {
                if (_idRegistry.TryGetValue(id, out var raceExisting))
                    return raceExisting as T;

                Task completed;
                if (timeoutMs >= 0)
                {
                    completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, cancellationToken));
                    if (!ReferenceEquals(completed, tcs.Task))
                        return null;
                }
                else
                {
                    completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cancellationToken));
                    if (!ReferenceEquals(completed, tcs.Task))
                        return null;
                }

                return await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                OnIdRegistered -= HandleRegistered;
            }
        }
        #endregion

        #region Reflection Helpers
        public void CallMethodByID(string id, string methodName, params object[] args)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(methodName))
            {
                return;
            }
            if (!_idRegistry.TryGetValue(id, out var target))
            {
                return;
            }

            var type = target.GetType();
            var cacheKey = (id, methodName);
            if (!_methodCache.TryGetValue(cacheKey, out var method))
            {
                method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                {
                    return;
                }
                _methodCache[cacheKey] = method;
            }

            try
            {
                method.Invoke(target, args);
                #if UNITY_EDITOR
                Debug.Log($"[{nameof(WorldBridgeSystem)}] Called {methodName} on {id}");
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{nameof(WorldBridgeSystem)}] Exception calling {methodName} on {id}: {ex.Message}");
            }
        }

        public int GetIntByID(string id, string member)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member)) return 0;
            if (!_idRegistry.TryGetValue(id, out var target)) return 0;

            var type = target.GetType();
            var key = (type, member);
            var getter = _boxedGetterCache.GetOrAdd(key, k => ExpressionHelpers.CreateBoxedGetter(k.Item1, k.Item2));
            try
            {
                var val = getter(target);
                return Convert.ToInt32(val);
            }
            catch
            {
                var obj = GetValueByID(id, member);
                return obj == null ? 0 : Convert.ToInt32(obj);
            }
        }

        public float GetFloatByID(string id, string member)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member)) return 0f;
            if (!_idRegistry.TryGetValue(id, out var target)) return 0f;

            var type = target.GetType();
            var key = (type, member);
            var getter = _boxedGetterCache.GetOrAdd(key, k => ExpressionHelpers.CreateBoxedGetter(k.Item1, k.Item2));
            try
            {
                var val = getter(target);
                return Convert.ToSingle(val);
            }
            catch
            {
                var obj = GetValueByID(id, member);
                return obj == null ? 0f : Convert.ToSingle(obj);
            }
        }

        public int GetShiftedIntByID(string id, string member, int shift, int bitCount)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member)) return 0;
            if (!_idRegistry.TryGetValue(id, out var target)) return 0;

            var type = target.GetType();
            try
            {
                var getter = ExpressionHelpers.GetOrCreateShiftedGetter(type, member, shift, bitCount);
                return getter(target);
            }
            catch
            {
                return unchecked((int)GetShiftedBitsByID(id, member, shift, bitCount));
            }
        }

        public ulong GetShiftedBitsByID(string id, string member, int shift, int bitCount)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member)) return 0UL;
            if (!_idRegistry.TryGetValue(id, out var target)) return 0UL;

            var type = target.GetType();
            try
            {
                var getter = ExpressionHelpers.GetOrCreateShiftedBitsGetter(type, member, shift, bitCount);
                return getter(target);
            }
            catch
            {
                var obj = GetValueByID(id, member);
                if (!FlagSwitchDynamic.TryConvertToBits(obj, out ulong bits))
                    return 0UL;
                if (shift < 0 || shift >= 64 || bitCount <= 0 || bitCount > 64 || shift + bitCount > 64)
                    return 0UL;

                ulong maskValue = bitCount == 64 ? ulong.MaxValue : ((1UL << bitCount) - 1UL);
                return (bits >> shift) & maskValue;
            }
        }

        public bool TryGetShiftedIntByID(string id, string member, int shift, int bitCount, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member)) return false;
            if (!_idRegistry.ContainsKey(id)) return false;

            try
            {
                value = GetShiftedIntByID(id, member, shift, bitCount);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        public bool TryGetShiftedBitsByID(string id, string member, int shift, int bitCount, out ulong value)
        {
            value = 0UL;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member)) return false;
            if (!_idRegistry.ContainsKey(id)) return false;

            try
            {
                value = GetShiftedBitsByID(id, member, shift, bitCount);
                return true;
            }
            catch
            {
                value = 0UL;
                return false;
            }
        }

        public bool RunFlagSwitchByID(string id, string member, FlagSwitchDynamic switcher)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member) || switcher == null)
                return false;
            if (!_idRegistry.TryGetValue(id, out var target))
                return false;

            var type = target.GetType();
            var key = (type, member);
            var getter = _boxedGetterCache.GetOrAdd(key, k => ExpressionHelpers.CreateBoxedGetter(k.Item1, k.Item2));

            try
            {
                var raw = getter(target);
                return FlagSwitchDynamic.TryConvertToBits(raw, out ulong bits) && switcher.RunBits(bits);
            }
            catch
            {
                var obj = GetValueByID(id, member);
                return FlagSwitchDynamic.TryConvertToBits(obj, out ulong bits) && switcher.RunBits(bits);
            }
        }

        public bool SetIntByID(string id, string member, int value)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member)) return false;
            if (!_idRegistry.TryGetValue(id, out var target)) return false;

            var type = target.GetType();
            var key = (type, member);
            var setter = _boxedSetterCache.GetOrAdd(key, k => ExpressionHelpers.CreateBoxedSetter(k.Item1, k.Item2));
            try
            {
                setter(target, value);
                return true;
            }
            catch
            {
                return SetValueByID(id, member, value);
            }
        }

        public bool SetFloatByID(string id, string member, float value)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member)) return false;
            if (!_idRegistry.TryGetValue(id, out var target)) return false;

            var type = target.GetType();
            var key = (type, member);
            var setter = _boxedSetterCache.GetOrAdd(key, k => ExpressionHelpers.CreateBoxedSetter(k.Item1, k.Item2));
            try
            {
                setter(target, value);
                return true;
            }
            catch
            {
                return SetValueByID(id, member, value);
            }
        }

        public T GetFieldByID<T>(string id, string member)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member)) return default(T);
            if (!_idRegistry.TryGetValue(id, out var target)) return default(T);

            var type = target.GetType();
            var key = (type, member);
            var getter = _boxedGetterCache.GetOrAdd(key, k => ExpressionHelpers.CreateBoxedGetter(k.Item1, k.Item2));
            try
            {
                var val = getter(target);
                if (val is T typedValue) return typedValue;

                var obj = GetValueByID(id, member);
                return obj is T reflectionValue ? reflectionValue : default(T);
            }
            catch
            {
                var obj = GetValueByID(id, member);
                return obj is T reflectionValue ? reflectionValue : default(T);
            }
        }

        public bool SetFieldByID<T>(string id, string member, T value)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member)) return false;
            if (!_idRegistry.TryGetValue(id, out var target)) return false;

            var type = target.GetType();
            var key = (type, member);
            var setter = _boxedSetterCache.GetOrAdd(key, k => ExpressionHelpers.CreateBoxedSetter(k.Item1, k.Item2));
            try
            {
                setter(target, value);
                return true;
            }
            catch
            {
                return SetValueByID(id, member, value);
            }
        }

        public bool SetValueByID(string id, string memberName, object value)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(memberName))
            {
                return false;
            }
            if (!_idRegistry.TryGetValue(id, out var target))
            {
                return false;
            }

            var type = target.GetType();
            var cacheKey = (id, memberName);
            if (!_memberCache.TryGetValue(cacheKey, out var member))
            {
                var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    member = field;
                }
                else
                {
                    var prop = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.CanWrite)
                        member = prop;
                }
                if (member == null)
                {
                    return false;
                }
                _memberCache[cacheKey] = member;
            }

            try
            {
                if (member is FieldInfo field)
                    field.SetValue(target, value);
                else if (member is PropertyInfo prop)
                    prop.SetValue(target, value);
                #if UNITY_EDITOR
                Debug.Log($"[{nameof(WorldBridgeSystem)}] Set {memberName} on {id} to {value}");
                #endif
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{nameof(WorldBridgeSystem)}] Failed to set {memberName} on {id}: {ex.Message}");
                return false;
            }
        }

        public object GetValueByID(string id, string memberName)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(memberName))
            {
                return null;
            }
            if (!_idRegistry.TryGetValue(id, out var target))
            {
                return null;
            }

            var type = target.GetType();
            var cacheKey = (id, memberName);
            if (!_memberCache.TryGetValue(cacheKey, out var member))
            {
                var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    member = field;
                }
                else
                {
                    var prop = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.CanRead)
                        member = prop;
                }
                if (member == null)
                {
                    return null;
                }
                _memberCache[cacheKey] = member;
            }

            try
            {
                if (member is FieldInfo field)
                    return field.GetValue(target);
                if (member is PropertyInfo prop)
                    return prop.GetValue(target);
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{nameof(WorldBridgeSystem)}] Failed to get {memberName} on {id}: {ex.Message}");
                return null;
            }
        }
        #endregion

        #region Invoker Convenience
        public IDisposable RegisterInvoker(string key, Action<object[]> method, DynamicDictionaryInvoker.Layer layer = DynamicDictionaryInvoker.Layer.Func, string id = null, object metadata = null)
        {
            if (_invoker == null)
            {
                return null;
            }
            return _invoker.Register(key, method, layer, id, metadata);
        }

        public IDisposable RegisterInvokerReturn(string key, Func<object[], object> method, DynamicDictionaryInvoker.Layer layer = DynamicDictionaryInvoker.Layer.Func, string id = null, object metadata = null)
        {
            if (_invoker == null) return null;
            return _invoker.RegisterReturn(key, method, layer, id, metadata);
        }

        public bool PayInvoke(string key, object token = null, params object[] args)
        {
            if (_invoker == null)
            {
                return false;
            }
            return _invoker.Pay(key, token, args);
        }

        public bool HasInvoker(string key)
        {
            if (_invoker == null)
                return false;
            return _invoker.HasInvoker(key);
        }

        public void InvokeKey(string key, params object[] args)
        {
            if (_invoker == null)
            {
                return;
            }
            _invoker.Invoke(key, args);
        }

        public object[] InvokeKeyReturn(string key, params object[] args)
        {
            if (_invoker == null) return Array.Empty<object>();
            return _invoker.InvokeReturn(key, args);
        }

        public object InvokeKeyReturnFirst(string key, params object[] args)
        {
            if (_invoker == null) return null;
            return _invoker.InvokeReturnFirst(key, args);
        }

        public void InvokeSafeKey(string key, params object[] args)
        {
            if (_invoker == null)
            {
                return;
            }
            _invoker.InvokeSafe(key, args);
        }

        /// <summary>
        /// Invoke a key once and then remove all handlers registered under that key.
        /// </summary>
        public void InvokeOnceKey(string key, params object[] args)
        {
            if (_invoker == null) return;
            _invoker.InvokeOnce(key, args);
        }

        /// <summary>
        /// Remove all registered invocation entries that match the given entry id across all keys.
        /// Returns the number of removed entries.
        /// </summary>
        public int RemoveAllEntriesForId(string entryId)
        {
            if (_invoker == null) return 0;
            return _invoker.RemoveAllEntriesForId(entryId);
        }
        #endregion

        #region Debug
        public void PrintRegistry()
        {
            #if UNITY_EDITOR
            Debug.Log($"[{nameof(WorldBridgeSystem)}] ID registry ({_idRegistry.Count} entries):");
            foreach (var kv in _idRegistry)
                Debug.Log($"{kv.Key} -> {kv.Value?.name} ({kv.Value?.GetType().Name ?? "null"})");
            #endif
        }
        #endregion
    }

    /// <summary>
    /// WorldBridgeRouter — small MonoBehaviour that routes WorldBridge invoker events
    /// to a registered target method by ID. This reduces boilerplate when you need
    /// to keep systems decoupled but still forward events to concrete MonoBehaviours.
    ///
    /// Example: add a `WorldBridgeRouter` to a scene, create a route for "player_aim_target_changed"
    /// and set TargetID to the UniqueId/registered id of a component. The router will call `TargetMethod`
    /// on the registered target with the event payload.
    ///
    /// Notes:
    /// - This uses `WorldBridgeSystem.Instance.RegisterInvoker` and `WorldBridgeSystem.CallMethodByID`
    /// - `CallMethodByID` uses reflection, so keep payloads simple (string, Vector3) or call typed wrappers.
    /// </summary>
    public class WorldBridgeRouter : MonoBehaviour
    {
        [Serializable]
        public class RouteEntry
        {
            public string EventKey;
            public string TargetId;
            public string TargetMethod;
            public DynamicDictionaryInvoker.Layer Layer = DynamicDictionaryInvoker.Layer.Func;
            public string DebugId;
            public string Metadata;

            // Allow enabling/disabling routes individually
            public bool Enabled = true;
        }

        [Tooltip("Configure event -> target routing." + " Example: 'player_aim_target_changed' -> 'aim_responder_01'.")]
        public List<RouteEntry> routes = new List<RouteEntry>();

        // Keep registration tokens to unsubscribe
        private readonly Dictionary<RouteEntry, IDisposable> _routeTokens = new Dictionary<RouteEntry, IDisposable>();

        private void OnEnable()
        {
            RegisterAll();
        }

        private void OnDisable()
        {
            UnregisterAll();
        }

        // Register all routes (called on start or when routes change)
        public void RegisterAll()
        {
            UnregisterAll();

            if (WorldBridgeSystem.Instance == null) return;

            foreach (var r in routes)
            {
                if (!r.Enabled) continue;

                // Capture local variable for closure
                var entry = r;

                var token = WorldBridgeSystem.Instance.RegisterInvoker(
                    entry.EventKey,
                    (object[] args) => {
                        TryCallRoute(entry, args);
                    },
                    entry.Layer,
                    id: entry.DebugId,
                    metadata: entry.Metadata
                );

                if (token != null) _routeTokens[entry] = token;
            }
        }

        public void UnregisterAll()
        {
            foreach (var kv in _routeTokens)
            {
                kv.Value?.Dispose();
            }
            _routeTokens.Clear();
        }

        // Call method on the target id; wraps Try/Catch and logs
        private void TryCallRoute(RouteEntry entry, object[] args)
        {
            try
            {
                // Route to target id via WorldBridge reflection helper
                WorldBridgeSystem.Instance.CallMethodByID(entry.TargetId, entry.TargetMethod, args);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WorldBridgeRouter] Error calling method '{entry.TargetMethod}' on '{entry.TargetId}': {ex.Message}");
            }
        }

        // Allow adding a route at runtime (returns the token so caller can store or ignore)
        public IDisposable AddRoute(string eventKey, string targetId, string targetMethod, DynamicDictionaryInvoker.Layer layer = DynamicDictionaryInvoker.Layer.Func, string debugId = null, object metadata = null)
        {
            var entry = new RouteEntry
            {
                EventKey = eventKey,
                TargetId = targetId,
                TargetMethod = targetMethod,
                DebugId = debugId,
                Metadata = metadata?.ToString(),
                Layer = layer,
                Enabled = true
            };

            routes.Add(entry);

            if (WorldBridgeSystem.Instance != null)
            {
                var token = WorldBridgeSystem.Instance.RegisterInvoker(entry.EventKey, args => TryCallRoute(entry, args), entry.Layer, id: entry.DebugId, metadata: entry.Metadata);
                if (token != null) _routeTokens[entry] = token;
            }

            return _routeTokens.ContainsKey(entry) ? _routeTokens[entry] : null;
        }

        // Remove a route and dispose
        public void RemoveRoute(RouteEntry route)
        {
            if (route == null) return;
            if (_routeTokens.TryGetValue(route, out var token)) token?.Dispose();
            _routeTokens.Remove(route);
            routes.Remove(route);
        }
    }

    public readonly struct BridgeResult
    {
        public readonly object Value;
        public readonly string Type;
        public readonly bool Success;

        public BridgeResult(object value, string type, bool success)
        {
            Value = value;
            Type = type;
            Success = success;
        }
    }

    public static class MethodWrapper
    {
        public static Action<object[]> Wrap<T1>(Action<T1> method)
            => args => method((T1)args[0]);

        public static Action<object[]> Wrap<T1, TResult>(Func<T1, TResult> method)
            => args => _ = method((T1)args[0]);

        public static Action<object[]> Wrap<T1, T2>(Action<T1, T2> method)
            => args => method((T1)args[0], (T2)args[1]);

        public static Action<object[]> Wrap<T1, T2, T3>(Action<T1, T2, T3> method)
            => args => method((T1)args[0], (T2)args[1], (T3)args[2]);

        //returnables and snapshots
        public static Func<object[], object> WrapReturn<T1, TResult>(Func<T1, TResult> method)
            => args => method((T1)args[0]);

        public static Func<object[], object> WrapReturn<TResult>(Func<TResult> method)
            => args => method();
    }

    public abstract class LogicNode
    {
        public bool Value { get; protected set; }
        public abstract bool Evaluate();

        public IDisposable Bind(Func<bool> getter)
        {
            var token = new NodeToken(() => _getter = null);
            _getter = getter;
            return token;
        }

        protected Func<bool> _getter;
    }

    public sealed class NodeToken : IDisposable
    {
        private Action _dispose;
        private bool _done;

        public NodeToken(Action dispose) => _dispose = dispose;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            _dispose?.Invoke();
            _dispose = null;
        }
    }

    public sealed class AndNode : LogicNode
    {
        private readonly List<Func<bool>> _inputs = new();

        public IDisposable AddInput(Func<bool> getter)
        {
            _inputs.Add(getter);
            return new NodeToken(() => _inputs.Remove(getter));
        }

        public override bool Evaluate()
        {
            Value = true;
            foreach (var input in _inputs)
            {
                if (!input()) { Value = false; break; }
            }
            return Value;
        }
    }

    public sealed class OrNode : LogicNode
    {
        private readonly List<Func<bool>> _inputs = new();

        public IDisposable AddInput(Func<bool> getter)
        {
            _inputs.Add(getter);
            return new NodeToken(() => _inputs.Remove(getter));
        }

        public override bool Evaluate()
        {
            Value = false;
            foreach (var input in _inputs)
            {
                if (input()) { Value = true; break; }
            }
            return Value;
        }
    }
    public sealed class XorNode : LogicNode
    {
        private readonly List<Func<bool>> _inputs = new();

        public IDisposable AddInput(Func<bool> getter)
        {
            _inputs.Add(getter);
            return new NodeToken(() => _inputs.Remove(getter));
        }

        // Parity XOR: true when an odd number of inputs evaluate true
        public override bool Evaluate()
        {
            int trueCount = 0;
            foreach (var input in _inputs)
            {
                try { if (input()) trueCount++; }
                catch { }
            }
            Value = (trueCount & 1) == 1;
            return Value;
        }
    }

    public sealed class NotNode : LogicNode
    {
        public IDisposable SetInput(Func<bool> getter)
        {
            _getter = getter;
            return new NodeToken(() => _getter = null);
        }

        public override bool Evaluate()
        {
            Value = !(_getter?.Invoke() ?? false);
            return Value;
        }
    }

    public sealed class BoolEventNode : LogicNode
    {
        private bool _bool;

        public void Raise(bool value)
        {
            _bool = value;
            Value = value;
        }

        public override bool Evaluate() => Value;
    }
}
