using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using FracturedStudios.Invoker;

namespace FracturedStudios.Utils
{
    /// <summary>
    /// Small set of helpers to build and compile Expression trees for common inspector/runtime access patterns.
    /// Includes an overridable builder base and convenience getters/setters that work on boxed `object` instances.
    /// </summary>
    public abstract class ExpressionBuilder
    {
        // Boxed instance parameter (Func<object,object> style helpers use this)
        protected virtual ParameterExpression Instance { get; set; } = Expression.Parameter(typeof(object), "instance");

        // Optional UI parameter shown in the snippet request
        protected virtual ParameterExpression UI { get; set; } = Expression.Parameter(typeof(object), "ui");

        // Derived classes provide a body expression. Default returns the UI parameter.
        protected virtual Expression BuildExpressionBody()
        {
            return UI;
        }

        // Compile a simple accessor: Func<object, object>
        public virtual Func<object, object> CompileUIAccessor()
        {
            var body = Expression.Convert(BuildExpressionBody(), typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(body, UI);
            return lambda.Compile();
        }

        // Invoke the compiled accessor immediately
        public virtual object InvokeUI(object uiInstance)
        {
            var accessor = CompileUIAccessor();
            return accessor(uiInstance);
        }
    }

    /// <summary>
    /// Static helpers to construct common member getters/setters as boxed Func/Action delegates.
    /// Works by accepting a runtime <see cref="Type"/> and a dot-delimited member path like "body.head.leftEye".
    /// </summary>
    public static class ExpressionHelpers
    {
        private readonly struct ExpBridgeKey : IEquatable<ExpBridgeKey>
        {
            public readonly Type Type;
            public readonly string PathInterned;
            public readonly int Shift;
            public readonly int Bits;

            public ExpBridgeKey(Type type, string pathInterned, int shift, int bits)
            {
                Type = type;
                PathInterned = pathInterned;
                Shift = shift;
                Bits = bits;
            }

            public bool Equals(ExpBridgeKey other)
            {
                return Type == other.Type
                    && PathInterned == other.PathInterned
                    && Shift == other.Shift
                    && Bits == other.Bits;
            }

            public override bool Equals(object obj)
            {
                return obj is ExpBridgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Type != null ? Type.GetHashCode() : 0;
                    hash = (hash * 397) ^ (PathInterned != null ? PathInterned.GetHashCode() : 0);
                    hash = (hash * 397) ^ Shift;
                    hash = (hash * 397) ^ Bits;
                    return hash;
                }
            }
        }

        private static readonly Dictionary<ExpBridgeKey, Func<object, int>> IntCache = new Dictionary<ExpBridgeKey, Func<object, int>>();
        private static readonly Dictionary<ExpBridgeKey, Func<object, ulong>> BitsCache = new Dictionary<ExpBridgeKey, Func<object, ulong>>();

        // Build and compile a boxed getter: Func<object, object>
        public static Func<object, object> CreateBoxedGetter(Type targetType, string memberPath)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrEmpty(memberPath)) throw new ArgumentNullException(nameof(memberPath));

            var param = Expression.Parameter(typeof(object), "instance");
            Expression current = Expression.Convert(param, targetType);
            foreach (var part in memberPath.Split('.'))
            {
                current = Expression.PropertyOrField(current, part);
            }
            var body = Expression.Convert(current, typeof(object));
            return Expression.Lambda<Func<object, object>>(body, param).Compile();
        }

        // Generic typed getter: Func<TTarget, TResult>
        public static Func<TTarget, TResult> CreateGetter<TTarget, TResult>(string memberPath)
        {
            var param = Expression.Parameter(typeof(TTarget), "target");
            Expression current = param;
            foreach (var part in memberPath.Split('.'))
            {
                current = Expression.PropertyOrField(current, part);
            }
            var body = Expression.Convert(current, typeof(TResult));
            return Expression.Lambda<Func<TTarget, TResult>>(body, (ParameterExpression)param).Compile();
        }

        public static Func<object, ulong> CreateShiftedBitsGetter(Type targetType, string memberPath, int shift, int bitCount)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrWhiteSpace(memberPath)) throw new ArgumentNullException(nameof(memberPath));
            if (shift < 0) throw new ArgumentOutOfRangeException(nameof(shift));
            if (bitCount <= 0 || bitCount > 64) throw new ArgumentOutOfRangeException(nameof(bitCount));
            if (shift >= 64) throw new ArgumentOutOfRangeException(nameof(shift));
            if (shift + bitCount > 64) throw new ArgumentOutOfRangeException(nameof(bitCount));

            var param = Expression.Parameter(typeof(object), "instance");
            Expression current = Expression.Convert(param, targetType);
            foreach (var part in memberPath.Split('.'))
            {
                current = Expression.PropertyOrField(current, part);
            }

            var valueType = Nullable.GetUnderlyingType(current.Type) ?? current.Type;
            if (!(valueType.IsEnum
                || valueType == typeof(byte)
                || valueType == typeof(sbyte)
                || valueType == typeof(short)
                || valueType == typeof(ushort)
                || valueType == typeof(int)
                || valueType == typeof(uint)
                || valueType == typeof(long)
                || valueType == typeof(ulong)))
            {
                throw new ArgumentException($"Member '{memberPath}' on type {targetType.FullName} is not an integral or enum value.");
            }

            var underlyingType = valueType.IsEnum ? Enum.GetUnderlyingType(valueType) : valueType;
            Expression numeric = current.Type == underlyingType ? current : Expression.Convert(current, underlyingType);

            ulong maskValue = bitCount == 64 ? ulong.MaxValue : ((1UL << bitCount) - 1UL);
            var asUInt64 = Expression.Convert(numeric, typeof(ulong));
            var shifted = Expression.RightShift(asUInt64, Expression.Constant(shift));
            var masked = Expression.And(shifted, Expression.Constant(maskValue));

            return Expression.Lambda<Func<object, ulong>>(masked, param).Compile();
        }

        public static Func<object, int> CreateShiftedIntGetter(Type targetType, string memberPath, int shift, int bitCount)
        {
            if (bitCount <= 0 || bitCount > 32) throw new ArgumentOutOfRangeException(nameof(bitCount));

            var bitsGetter = CreateShiftedBitsGetter(targetType, memberPath, shift, bitCount);
            return instance => unchecked((int)bitsGetter(instance));
        }

        public static Func<object, ulong> GetOrCreateShiftedBitsGetter(Type type, string path, int shift, int bits)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            var key = new ExpBridgeKey(type, string.Intern(path), shift, bits);

            if (BitsCache.TryGetValue(key, out var getter))
                return getter;

            getter = CreateShiftedBitsGetter(type, path, shift, bits);
            BitsCache[key] = getter;
            return getter;
        }

        public static Func<object, int> GetOrCreateShiftedGetter(Type type, string path, int shift, int bits)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            var key = new ExpBridgeKey(type, string.Intern(path), shift, bits);

            if (IntCache.TryGetValue(key, out var getter))
                return getter;

            getter = CreateShiftedIntGetter(type, path, shift, bits);
            IntCache[key] = getter;
            return getter;
        }

        public static Func<object, bool> CreateFlagBoolGetter(Type type, string path, RouteScope flag)
        {
            return CreateFlagBoolGetter(type, path, 8, 8, flag);
        }

        public static Func<object, bool> CreateFlagBoolGetter(Type type, string path, int shift, int bitCount, RouteScope flag)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            var getter = GetOrCreateShiftedGetter(type, path, shift, bitCount);
            return obj => ScopeHelper.HasAny(ScopeHelper.GetScope(obj, getter), flag);
        }

        public static Func<object, bool> CreateBitBoolGetter(Type targetType, string memberPath, int bitIndex)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrWhiteSpace(memberPath)) throw new ArgumentNullException(nameof(memberPath));
            if (bitIndex < 0 || bitIndex > 63) throw new ArgumentOutOfRangeException(nameof(bitIndex));

            var getter = GetOrCreateShiftedBitsGetter(targetType, memberPath, bitIndex, 1);
            return obj => getter(obj) != 0UL;
        }

        public static Func<string, ulong> CreateWorldShiftedBitsGetter(string memberPath, int shift, int bitCount)
        {
            if (string.IsNullOrWhiteSpace(memberPath)) throw new ArgumentNullException(nameof(memberPath));

            return id =>
            {
                var bridge = WorldBridgeSystem.Instance;
                if (bridge == null || string.IsNullOrWhiteSpace(id))
                    return 0UL;

                var obj = bridge.GetByID<UnityEngine.Object>(id);
                if (obj == null)
                    return 0UL;

                var getter = GetOrCreateShiftedBitsGetter(obj.GetType(), memberPath, shift, bitCount);
                return getter(obj);
            };
        }

        public static Func<string, int> CreateWorldShiftedGetter(string memberPath, int shift, int bitCount)
        {
            if (string.IsNullOrWhiteSpace(memberPath)) throw new ArgumentNullException(nameof(memberPath));

            return id =>
            {
                var bridge = WorldBridgeSystem.Instance;
                if (bridge == null || string.IsNullOrWhiteSpace(id))
                    return 0;

                var obj = bridge.GetByID<UnityEngine.Object>(id);
                if (obj == null)
                    return 0;

                var getter = GetOrCreateShiftedGetter(obj.GetType(), memberPath, shift, bitCount);
                return getter(obj);
            };
        }

        public static Func<string, bool> CreateWorldFlagRunner(string memberPath, FlagSwitchDynamic switcher)
        {
            if (string.IsNullOrWhiteSpace(memberPath)) throw new ArgumentNullException(nameof(memberPath));
            if (switcher == null) throw new ArgumentNullException(nameof(switcher));

            string internedPath = string.Intern(memberPath);
            var getterCache = new Dictionary<Type, Func<object, object>>();

            return id =>
            {
                var bridge = WorldBridgeSystem.Instance;
                if (bridge == null || string.IsNullOrWhiteSpace(id))
                    return false;

                var obj = bridge.GetByID<UnityEngine.Object>(id);
                if (obj == null)
                    return false;

                Type type = obj.GetType();
                if (!getterCache.TryGetValue(type, out var getter))
                {
                    getter = CreateBoxedGetter(type, internedPath);
                    getterCache[type] = getter;
                }

                object raw = getter(obj);
                return FlagSwitchDynamic.TryConvertToBits(raw, out ulong bits) && switcher.RunBits(bits);
            };
        }

        // Build and compile a boxed setter: Action<object, object>
        public static Action<object, object> CreateBoxedSetter(Type targetType, string memberPath)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrEmpty(memberPath)) throw new ArgumentNullException(nameof(memberPath));

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var valueParam = Expression.Parameter(typeof(object), "value");

            Expression current = Expression.Convert(instanceParam, targetType);
            var parts = memberPath.Split('.');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                current = Expression.PropertyOrField(current, parts[i]);
            }

            var lastPart = parts[parts.Length - 1];
            var member = Expression.PropertyOrField(current, lastPart);
            var assign = Expression.Assign(member, Expression.Convert(valueParam, member.Type));
            var lambda = Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam);
            return lambda.Compile();
        }

        // Generic typed setter: Action<TTarget, TValue>
        public static Action<TTarget, TValue> CreateSetter<TTarget, TValue>(string memberPath)
        {
            var instanceParam = Expression.Parameter(typeof(TTarget), "instance");
            var valueParam = Expression.Parameter(typeof(TValue), "value");
            Expression current = instanceParam;
            var parts = memberPath.Split('.');
            for (int i = 0; i < parts.Length - 1; i++) current = Expression.PropertyOrField(current, parts[i]);
            var last = Expression.PropertyOrField(current, parts[parts.Length - 1]);
            var assign = Expression.Assign(last, Expression.Convert(valueParam, last.Type));
            return Expression.Lambda<Action<TTarget, TValue>>(assign, instanceParam, valueParam).Compile();
        }

        // Utility: try to read a value via reflection if expression build fails (safe fallback)
        public static object ReflectionGet(object target, string memberPath)
        {
            if (target == null) return null;
            object cur = target;
            foreach (var part in memberPath.Split('.'))
            {
                if (cur == null) return null;
                var t = cur.GetType();
                var fi = t.GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null) { cur = fi.GetValue(cur); continue; }
                var pi = t.GetProperty(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null) { cur = pi.GetValue(cur); continue; }
                return null;
            }
            return cur;
        }
    }

    public sealed class FlagSwitchDynamic
    {
        private readonly struct Entry
        {
            public readonly ulong Mask;
            public readonly ulong Required;
            public readonly Action Action;

            public Entry(ulong mask, ulong required, Action action)
            {
                Mask = mask;
                Required = required;
                Action = action;
            }
        }

        private readonly List<Entry> _entries = new List<Entry>();

        public int Count => _entries.Count;

        public void Clear()
        {
            _entries.Clear();
        }

        public void Add<TEnum>(TEnum mask, TEnum required, Action action)
            where TEnum : struct, Enum
        {
            AddBits(ToBits(mask), ToBits(required), action);
        }

        public void AddBits(ulong mask, ulong required, Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _entries.Add(new Entry(mask, required, action));
        }

        public bool Run<TEnum>(TEnum current)
            where TEnum : struct, Enum
        {
            return RunBits(ToBits(current));
        }

        public bool RunBits(ulong current)
        {
            bool anyExecuted = false;

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if ((current & entry.Mask) != entry.Required)
                    continue;

                entry.Action?.Invoke();
                anyExecuted = true;
            }

            return anyExecuted;
        }

        public static bool TryConvertToBits(object value, out ulong bits)
        {
            bits = 0UL;
            if (value == null)
                return false;

            try
            {
                bits = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ulong ToBits<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        }
    }

    [Flags]
    public enum RouteScope : uint
    {
        None = 0u,
        Local = 1u << 0,
        Global = 1u << 1,
        Persistent = 1u << 2,
        Transient = 1u << 3,
        Authenticated = 1u << 4,
        AllInternal = Local | Transient,
        Everything = 0xFFFFFFFFu
    }

    public static class ScopeHelper
    {
        public static bool IsInScope(RouteScope current, RouteScope target)
        {
            return (current & target) != 0;
        }

        public static bool MatchesAll(RouteScope current, RouteScope required)
        {
            return (current & required) == required;
        }

        public static RouteScope Add(RouteScope current, RouteScope value)
        {
            return current | value;
        }

        public static RouteScope Remove(RouteScope current, RouteScope value)
        {
            return current & ~value;
        }

        public static RouteScope Toggle(RouteScope current, RouteScope value)
        {
            return current ^ value;
        }

        public static bool HasAny(RouteScope current, RouteScope flags)
        {
            return IsInScope(current, flags);
        }

        public static bool HasAll(RouteScope current, RouteScope flags)
        {
            return MatchesAll(current, flags);
        }

        public static RouteScope GetScope(object obj, Func<object, int> getter)
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            return (RouteScope)getter(obj);
        }

        public static RouteScope ExtractScopeFromId(string id, RouteScope fallback = RouteScope.None)
        {
            if (string.IsNullOrWhiteSpace(id))
                return fallback;

            int separatorIndex = id.LastIndexOfAny(new[] { '|', '@', '#' });
            if (separatorIndex < 0 || separatorIndex >= id.Length - 1)
                return fallback;

            return ParseScope(id.Substring(separatorIndex + 1), fallback);
        }

        public static void InvokeScoped(string key, string id, RouteScope required)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(id))
                return;

            RouteScope scope = ExtractScopeFromId(id, RouteScope.None);
            if (!HasAll(scope, required))
                return;

            WorldBridgeSystem.Instance?.InvokeKey(key, id);
        }

        public static RouteScope ParseScope(string input, RouteScope fallback = RouteScope.Local)
        {
            if (string.IsNullOrWhiteSpace(input)) return fallback;

            var trimmed = input.Trim();

            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (ulong.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue))
                    return (RouteScope)hexValue;
            }

            if (ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue))
                return (RouteScope)numericValue;

            if (Enum.TryParse<RouteScope>(trimmed, true, out var parsed))
                return parsed;

            return fallback;
        }
    }
}
