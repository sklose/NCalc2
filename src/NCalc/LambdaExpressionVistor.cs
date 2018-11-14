using System;
using System.Linq;
using System.Reflection;
using NCalc.Domain;
using L = System.Linq.Expressions;
using System.Collections.Generic;

namespace NCalc
{
    /// <summary>
    ///     Implements creating <see cref="L.Expression"/> (expression trees) for formula.
    /// </summary>
    internal class LambdaExpressionVistor : LogicalExpressionVisitor
    {
        private static readonly Type MathType = typeof(Math);

        private readonly IDictionary<string, object> _parameters;
        private readonly L.Expression _context;
        private readonly EvaluateOptions _options;

        private bool Ordinal { get { return (_options & EvaluateOptions.MatchStringsOrdinal) == EvaluateOptions.MatchStringsOrdinal; } }
        private bool IgnoreCaseString { get { return (_options & EvaluateOptions.MatchStringsWithIgnoreCase) == EvaluateOptions.MatchStringsWithIgnoreCase; } }
        private bool Checked { get { return (_options & EvaluateOptions.OverflowProtection) == EvaluateOptions.OverflowProtection; } }

        public LambdaExpressionVistor(IDictionary<string, object> parameters, EvaluateOptions options)
        {
            _parameters = parameters;
            _options = options;
        }

        public LambdaExpressionVistor(L.ParameterExpression context, EvaluateOptions options)
        {
            _context = context;
            _options = options;
        }

        /// <summary>
        ///     Occurs when a parameter needs to be resolved.
        /// </summary>
        /// <remarks>
        ///     This event handler can be used to extend the usable parameters in the formula.
        /// </remarks>
        public event EventHandler<ParameterExpressionEventArgs> EvaluateParameter;

        /// <summary>
        ///     Occurs when a function needs to be resolved.
        /// </summary>
        /// <remarks>
        ///     This event handler can be used to extend the usable functions in the formula.
        /// </remarks>
        public event EventHandler<FunctionExpressionEventArgs> EvaluateFunction;

        public L.Expression Result { get; private set; }

        public override void Visit(LogicalExpression expression)
        {
            throw new NotImplementedException();
        }

        public override void Visit(TernaryExpression expression)
        {
            expression.LeftExpression.Accept(this);
            var test = Result;

            expression.MiddleExpression.Accept(this);
            var ifTrue = Result;

            expression.RightExpression.Accept(this);
            var ifFalse = Result;

            Result = L.Expression.Condition(test, ifTrue, ifFalse);
        }

        public override void Visit(BinaryExpression expression)
        {
            expression.LeftExpression.Accept(this);
            var left = Result;

            expression.RightExpression.Accept(this);
            var right = Result;

            switch (expression.Type)
            {
                case BinaryExpressionType.And:
                    Result = L.Expression.AndAlso(left, right);
                    break;
                case BinaryExpressionType.Or:
                    Result = L.Expression.OrElse(left, right);
                    break;
                case BinaryExpressionType.NotEqual:
                    Result = WithCommonNumericType(left, right, L.Expression.NotEqual, expression.Type);
                    break;
                case BinaryExpressionType.LesserOrEqual:
                    Result = WithCommonNumericType(left, right, L.Expression.LessThanOrEqual, expression.Type);
                    break;
                case BinaryExpressionType.GreaterOrEqual:
                    Result = WithCommonNumericType(left, right, L.Expression.GreaterThanOrEqual, expression.Type);
                    break;
                case BinaryExpressionType.Lesser:
                    Result = WithCommonNumericType(left, right, L.Expression.LessThan, expression.Type);
                    break;
                case BinaryExpressionType.Greater:
                    Result = WithCommonNumericType(left, right, L.Expression.GreaterThan, expression.Type);
                    break;
                case BinaryExpressionType.Equal:
                    Result = WithCommonNumericType(left, right, L.Expression.Equal, expression.Type);
                    break;
                case BinaryExpressionType.Minus:
                    if (Checked) Result = WithCommonNumericType(left, right, L.Expression.SubtractChecked);
                    else Result = WithCommonNumericType(left, right, L.Expression.Subtract);
                    break;
                case BinaryExpressionType.Plus:
                    if (Checked) Result = WithCommonNumericType(left, right, L.Expression.AddChecked);
                    else Result = WithCommonNumericType(left, right, L.Expression.Add);
                    break;
                case BinaryExpressionType.Modulo:
                    Result = WithCommonNumericType(left, right, L.Expression.Modulo);
                    break;
                case BinaryExpressionType.Div:
                    Result = WithCommonNumericType(left, right, L.Expression.Divide);
                    break;
                case BinaryExpressionType.Times:
                    if (Checked) Result = WithCommonNumericType(left, right, L.Expression.MultiplyChecked);
                    else Result = WithCommonNumericType(left, right, L.Expression.Multiply);
                    break;
                case BinaryExpressionType.BitwiseOr:
                    Result = L.Expression.Or(left, right);
                    break;
                case BinaryExpressionType.BitwiseAnd:
                    Result = L.Expression.And(left, right);
                    break;
                case BinaryExpressionType.BitwiseXOr:
                    Result = L.Expression.ExclusiveOr(left, right);
                    break;
                case BinaryExpressionType.LeftShift:
                    Result = L.Expression.LeftShift(left, right);
                    break;
                case BinaryExpressionType.RightShift:
                    Result = L.Expression.RightShift(left, right);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"The expression type '{expression.Type}' is not yet supported.");
            }
        }

        public override void Visit(UnaryExpression expression)
        {
            expression.Expression.Accept(this);
            switch (expression.Type)
            {
                case UnaryExpressionType.Not:
                    Result = L.Expression.Not(Result);
                    break;
                case UnaryExpressionType.Negate:
                    Result = L.Expression.Negate(Result);
                    break;
                case UnaryExpressionType.BitwiseNot:
                    Result = L.Expression.Not(Result);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"The expression type '{expression.Type}' is not yet supported.");
            }
        }

        public override void Visit(ValueExpression expression)
        {
            Result = L.Expression.Constant(expression.Value);
        }

        public override void Visit(Function function)
        {
            var functionArgs = new L.Expression[function.Expressions.Length];
            for (int i = 0; i < function.Expressions.Length; i++)
            {
                function.Expressions[i].Accept(this);
                functionArgs[i] = Result;
            }

            var extensionArgs = new FunctionExpressionEventArgs(function.Identifier.Name, functionArgs);
            EvaluateFunction?.Invoke(this, extensionArgs);
            if (extensionArgs.HasResult)
            {
                Result = extensionArgs.Result;
                return;
            }

            var functionName = function.Identifier.Name.ToLowerInvariant();
            switch (functionName)
            {
                case "if":
                    Result = L.Expression.Condition(functionArgs[0], functionArgs[1], functionArgs[2]);
                    break;
                case "in":
                    var items = L.Expression.NewArrayInit(functionArgs[0].Type,
                        new ArraySegment<L.Expression>(functionArgs, 1, functionArgs.Length - 1));
                    var smi = typeof(Array).GetRuntimeMethod("IndexOf", new[] { typeof(Array), typeof(object) });
                    var r = L.Expression.Call(smi, L.Expression.Convert(items, typeof(Array)), L.Expression.Convert(functionArgs[0], typeof(object)));
                    Result = L.Expression.GreaterThanOrEqual(r, L.Expression.Constant(0));
                    break;
                default:
                    if (_context != null)
                    {
                        var method = FindMethod(
                            _context.Type, function.Identifier.Name, functionArgs, ContextMethodPredicate);
                        if (method != null)
                        {
                            Result = L.Expression.Call(_context, method.BaseMethodInfo, method.PreparedArguments);
                            break;
                        }
                    }

                    var mathMethod = FindMethod(MathType, function.Identifier.Name, functionArgs, MathMethodPredicate);
                    if (mathMethod == null)
                    {
                        throw new MissingMethodException($"The function '{function.Identifier.Name}' was not defined.");
                    }

                    Result = L.Expression.Call(_context, mathMethod.BaseMethodInfo, mathMethod.PreparedArguments);
                    break;
            }
        }

        private bool ContextMethodPredicate(MethodInfo method)
        {
            return !method.IsStatic;
        }

        private bool MathMethodPredicate(MethodInfo arg)
        {
            return true;
        }

        public override void Visit(Identifier identifier)
        {
            if (_context != null)
            {
                Result = L.Expression.PropertyOrField(_context, identifier.Name);
                return;
            }

            if (_parameters.TryGetValue(identifier.Name, out var value))
            {
                Result = L.Expression.Constant(value);
                return;
            }

            // check for extension
            // The parameter should be defined in a call back method
            var args = new ParameterExpressionEventArgs(identifier.Name);

            // Calls external implementation
            EvaluateParameter?.Invoke(this, args);

            if (!args.HasResult)
            {
                throw new ArgumentException("Parameter was not defined.", identifier.Name);
            }

            Result = args.Result;
        }

        private ExtendedMethodInfo FindMethod(Type type, string methodName, L.Expression[] methodArgs, Func<MethodInfo, bool> methodPredicate)
        {
            var methods = type.GetTypeInfo().DeclaredMethods.Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) && m.IsPublic && methodPredicate(m));
            foreach (var potentialMethod in methods)
            {
                var methodParams = potentialMethod.GetParameters();
                var newArguments = PrepareMethodArgumentsIfValid(methodParams, methodArgs);

                if (newArguments != null)
                {
                    return new ExtendedMethodInfo { BaseMethodInfo = potentialMethod, PreparedArguments = newArguments };
                }
            }

            return null;
        }

        private L.Expression[] PrepareMethodArgumentsIfValid(ParameterInfo[] parameters, L.Expression[] arguments)
        {
            if (!parameters.Any() && !arguments.Any()) return arguments;
            if (!parameters.Any()) return null;

            var lastParameter = parameters.Last();
            bool hasParamsKeyword = lastParameter.IsDefined(typeof(ParamArrayAttribute));
            if (hasParamsKeyword && parameters.Length > arguments.Length) return null;
            L.Expression[] newArguments = new L.Expression[parameters.Length];
            L.Expression[] paramsKeywordArgument = null;
            Type paramsElementType = null;
            int paramsParameterPosition = 0;
            if (!hasParamsKeyword)
            {
                if (parameters.Length != arguments.Length) return null;
            }
            else
            {
                paramsParameterPosition = lastParameter.Position;
                paramsElementType = lastParameter.ParameterType.GetElementType();
                paramsKeywordArgument = new L.Expression[arguments.Length - parameters.Length + 1];
            }

            for (int i = 0; i < arguments.Length; i++)
            {
                var isParamsElement = hasParamsKeyword && i >= paramsParameterPosition;
                var argument = arguments[i];
                var argumentType = argument.Type.ToTypeCode();
                var parameterType = isParamsElement ? paramsElementType : parameters[i].ParameterType;
                var parameterTypeCode = parameterType.ToTypeCode();
                if (argumentType != parameterTypeCode && !CanConvert(argumentType, parameterTypeCode)) return null;
                if (!isParamsElement)
                {
                    if (argumentType != parameterTypeCode)
                    {
                        newArguments[i] = L.Expression.Convert(argument, parameterType);
                    }
                    else
                    {
                        newArguments[i] = argument;
                    }
                }
                else
                {
                    paramsKeywordArgument[i - paramsParameterPosition] = argument;
                }
            }

            if (hasParamsKeyword)
            {
                newArguments[paramsParameterPosition] = L.Expression.NewArrayInit(paramsElementType, paramsKeywordArgument);
            }
            return newArguments;
        }

        private bool CanConvert(TypeCode fromType, TypeCode toType)
        {
            if (toType == TypeCode.Double)
            {
                switch (fromType)
                {
                    case TypeCode.Int32:
                        return true;

                    default:
                        return false;
                }
            }

            return false;
        }

        private L.Expression WithCommonNumericType(L.Expression left, L.Expression right,
            Func<L.Expression, L.Expression, L.Expression> action, BinaryExpressionType expressiontype = BinaryExpressionType.Unknown)
        {
            left = UnwrapNullable(left);
            right = UnwrapNullable(right);

            if (_options.HasFlag(EvaluateOptions.BooleanCalculation))
            {
                if (left.Type == typeof(bool))
                {
                    left = L.Expression.Condition(left, L.Expression.Constant(1.0), L.Expression.Constant(0.0));
                }

                if (right.Type == typeof(bool))
                {
                    right = L.Expression.Condition(right, L.Expression.Constant(1.0), L.Expression.Constant(0.0));
                }
            }

            var precedence = new[]
            {
                typeof(decimal),
                typeof(double),
                typeof(float),
                typeof(ulong),
                typeof(long),
                typeof(uint),
                typeof(int),
                typeof(ushort),
                typeof(short),
                typeof(byte),
                typeof(sbyte)
            };

            int l = Array.IndexOf(precedence, left.Type);
            int r = Array.IndexOf(precedence, right.Type);
            if (l >= 0 && r >= 0)
            {
                var type = precedence[Math.Min(l, r)];
                if (left.Type != type)
                {
                    left = L.Expression.Convert(left, type);
                }

                if (right.Type != type)
                {
                    right = L.Expression.Convert(right, type);
                }
            }
            L.Expression comparer = null;
            if (IgnoreCaseString)
            {
                if (Ordinal) comparer = L.Expression.Property(null, typeof(StringComparer), "OrdinalIgnoreCase");
                else comparer = L.Expression.Property(null, typeof(StringComparer), "CurrentCultureIgnoreCase");
            }
            else comparer = L.Expression.Property(null, typeof(StringComparer), "Ordinal");

            if (comparer != null && (typeof(string).Equals(left.Type) || typeof(string).Equals(right.Type)))
            {
                switch (expressiontype)
                {
                    case BinaryExpressionType.Equal: return L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Equals", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right });
                    case BinaryExpressionType.NotEqual: return L.Expression.Not(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Equals", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right }));
                    case BinaryExpressionType.GreaterOrEqual: return L.Expression.GreaterThanOrEqual(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right }), L.Expression.Constant(0));
                    case BinaryExpressionType.LesserOrEqual: return L.Expression.LessThanOrEqual(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right }), L.Expression.Constant(0));
                    case BinaryExpressionType.Greater: return L.Expression.GreaterThan(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right }), L.Expression.Constant(0));
                    case BinaryExpressionType.Lesser: return L.Expression.LessThan(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right }), L.Expression.Constant(0));
                }
            }
            return action(left, right);
        }

        private L.Expression UnwrapNullable(L.Expression expression)
        {
            var ti = expression.Type.GetTypeInfo();
            if (ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof (Nullable<>))
            {
                return L.Expression.Condition(
                    L.Expression.Property(expression, "HasValue"),
                    L.Expression.Property(expression, "Value"),
                    L.Expression.Default(expression.Type.GetTypeInfo().GenericTypeArguments[0]));
            }

            return expression;
        }
    }
}
