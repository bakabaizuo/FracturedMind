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
   
        // Note: ExpressionHelpers (in FracturedStudios.Utils) is the single-source-of-truth
        // for building/compiling expression-based accessors. This class provides the
        // protected `UI` ParameterExpression and a virtual `BuildExpressionBody()` so
        // derived types can construct expression bodies; callers should use the
        // centralized helpers in ExpressionHelpers when they need compiled delegates.
    }
}
