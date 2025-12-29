using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using FracturedStudios.Data;

using FracturedStudios.Invoker;
namespace FracturedStudios.Invoker
{
    [DefaultExecutionOrder(-139)]
    public class WorldBridgeSystem : MonoBehaviour
    {
        public static WorldBridgeSystem Instance { get; private set; }

        // Maps stable IDs -> UnityEngine.Object (GameObject, Component, etc.)
        private readonly ConcurrentDictionary<string, UnityEngine.Object> _idRegistry = new();


        public PlayerData data;
        // Cache for reflection results to improve performance
        private readonly Dictionary<(string id, string member), MemberInfo> _memberCache = new();
        private readonly Dictionary<(string id, string method), MethodInfo> _methodCache = new();

        private DynamicDictionaryInvoker _invoker;
        //Version 2.0 
        //Remove Logs in build
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
                Debug.Log($"[{nameof(WorldBridgeSystem)}] Created {nameof(DynamicDictionaryInvoker)}");
            }
         
        
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
               
            
                _idRegistry.Clear();
                _memberCache.Clear();
                _methodCache.Clear();
                Instance = null;





            }
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
            Debug.Log($"[{nameof(WorldBridgeSystem)}] Registered ID {id} -> {target.name} ({target.GetType().Name})");
        }

        public void UnregisterID(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
               
                return;
            }
            if (_idRegistry.TryRemove(id, out _))
                Debug.Log($"[{nameof(WorldBridgeSystem)}] Unregistered ID {id}");
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
                Debug.Log($"[{nameof(WorldBridgeSystem)}] Called {methodName} on {id}");
            }
            catch (Exception)
            {
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
                Debug.Log($"[{nameof(WorldBridgeSystem)}] Set {memberName} on {id} to {value}");
                return true;
            }
            catch 
            {
                
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
            catch (Exception)
            {
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

        public void InvokeSafeKey(string key, params object[] args)
        {
            if (_invoker == null)
            {
                
                return;
            }
            _invoker.InvokeSafe(key, args);
        }
        #endregion

        #region Debug
        public void PrintRegistry()
        {
            Debug.Log($"[{nameof(WorldBridgeSystem)}] ID registry ({_idRegistry.Count} entries):");
            foreach (var kv in _idRegistry)
                Debug.Log($"{kv.Key} -> {kv.Value?.name} ({kv.Value?.GetType().Name ?? "null"})");
        }
        #endregion
    }
}