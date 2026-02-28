using System;
using System.Linq.Expressions;
using System.Reflection;

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
}
