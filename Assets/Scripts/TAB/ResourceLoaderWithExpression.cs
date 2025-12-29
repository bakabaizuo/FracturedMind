using System;
using System.Linq.Expressions;

namespace FracturedStudios.TAB
{
    /// <summary>
    /// Instance-style helper demonstrating an extensible Expression-based API.
    /// Provides a protected virtual `UI` ParameterExpression as requested and
    /// small helper methods showing how derived classes can build and compile
    /// simple expression lambdas that reference the `UI` parameter.
    /// </summary>
    public class ResourceLoaderWithExpression
    {
        // Protected virtual ParameterExpression with default initializer.
        protected virtual ParameterExpression UI { get; set; } = Expression.Parameter(typeof(object), "instance");

        // Allow derived classes to provide a body expression. Default simply returns the UI parameter.
        protected virtual Expression BuildExpressionBody()
        {
            // Default body: just return the UI parameter (converted to object)
            return UI;
        }

        // Compile a simple accessor: Func<object, object> that returns the instance passed in.
        public virtual Func<object, object> CompileUIAccessor()
        {
            var body = Expression.Convert(BuildExpressionBody(), typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(body, UI);
            return lambda.Compile();
        }

        // Example usage helper that invokes the compiled accessor immediately.
        public virtual object InvokeUI(object instance)
        {
            var accessor = CompileUIAccessor();
            return accessor(instance);
        }
    }
}
