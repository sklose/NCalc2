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
        private static readonly Type DoubleType = typeof(double);
        private static readonly Type BooleanType = typeof(bool);

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
                    if (SearchMethod(null, typeof(Math), functionName, functionArgs))
                    {
                        return;
                    }

                    if (_context == null)
                    {
                        throw new MissingMethodException($"The function '{function.Identifier.Name}' was not defined.");
                    }

                    if (SearchMethod(_context, _context.Type, functionName, functionArgs))
                    {
                        return;
                    }

                    throw new MissingMethodException($"The function '{function.Identifier.Name}' was not defined.");
            }
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

        private static L.Expression EnsureType(L.Expression expression, Type type)
        {
            if (expression.Type == type)
            {
                return expression;
            }

            if (type == BooleanType)
            {
                return L.Expression.GreaterThan(EnsureType(expression, DoubleType), L.Expression.Constant(0.0));
            }

            return L.Expression.Convert(expression, type);
        }

        private bool SearchMethod(L.Expression instance, Type type, string methodName,
            L.Expression[] methodArgs)
        {
            var methodCandidates = type.GetRuntimeMethods()
                .Where(x =>
                    x.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)
                    && x.GetParameters().Length == methodArgs.Length);

            // search for method with exactly matching parameters
            foreach (var methodCandidate in methodCandidates)
            {
                var methodParameters = methodCandidate.GetParameters();
                int matchCount = 0;
                for (int i = 0; i < methodArgs.Length; i++)
                {
                    if (methodParameters[i].ParameterType != methodArgs[i].Type)
                    {
                        continue;
                    }

                    matchCount++;
                }

                if (matchCount == methodArgs.Length)
                {
                    Result = instance == null
                        ? L.Expression.Call(methodCandidate, methodArgs)
                        : L.Expression.Call(instance, methodCandidate, methodArgs);
                    return true;
                }
            }

            // search for method using double and convert parameters
            var method = type.GetRuntimeMethods().FirstOrDefault(x =>
                x.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)
                && x.GetParameters().Length == methodArgs.Length
                && x.GetParameters().All(p => p.ParameterType == DoubleType));
            if (method == null)
            {
                return false;
            }

            var convertedArgs = methodArgs.Select(x => EnsureType(x, DoubleType)).ToArray();
            Result = instance == null
                ? L.Expression.Call(method, convertedArgs)
                : L.Expression.Call(instance, method, convertedArgs);
            return true;
        }

        private L.Expression WithCommonNumericType(L.Expression left, L.Expression right,
            Func<L.Expression, L.Expression, L.Expression> action, BinaryExpressionType expressiontype = BinaryExpressionType.Unknown)
        {
            left = UnwrapNullable(left);
            right = UnwrapNullable(right);

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
