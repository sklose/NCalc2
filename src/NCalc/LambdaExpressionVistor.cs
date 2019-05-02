using System;
using System.Linq;
using System.Reflection;
using NCalc.Domain;
using L = System.Linq.Expressions;
using System.Collections.Generic;

namespace NCalc
{
    internal class LambdaExpressionVistor : LogicalExpressionVisitor
    {
        private readonly IDictionary<string, object> _parameters;
        private L.Expression _result;
        private readonly L.Expression _context;
        private readonly EvaluateOptions _options = EvaluateOptions.None;

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

        public L.Expression Result => _result;

        public override void Visit(LogicalExpression expression)
        {
            throw new NotImplementedException();
        }

        public override void Visit(TernaryExpression expression)
        {
            expression.LeftExpression.Accept(this);
            var test = _result;

            expression.MiddleExpression.Accept(this);
            var ifTrue = _result;

            expression.RightExpression.Accept(this);
            var ifFalse = _result;

            _result = L.Expression.Condition(test, ifTrue, ifFalse);
        }

        public override void Visit(BinaryExpression expression)
        {
            expression.LeftExpression.Accept(this);
            var left = _result;

            expression.RightExpression.Accept(this);
            var right = _result;

            switch (expression.Type)
            {
                case BinaryExpressionType.And:
                    _result = L.Expression.AndAlso(left, right);
                    break;
                case BinaryExpressionType.Or:
                    _result = L.Expression.OrElse(left, right);
                    break;
                case BinaryExpressionType.NotEqual:
                    _result = WithCommonNumericType(left, right, L.Expression.NotEqual, expression.Type);
                    break;
                case BinaryExpressionType.LesserOrEqual:
                    _result = WithCommonNumericType(left, right, L.Expression.LessThanOrEqual, expression.Type);
                    break;
                case BinaryExpressionType.GreaterOrEqual:
                    _result = WithCommonNumericType(left, right, L.Expression.GreaterThanOrEqual, expression.Type);
                    break;
                case BinaryExpressionType.Lesser:
                    _result = WithCommonNumericType(left, right, L.Expression.LessThan, expression.Type);
                    break;
                case BinaryExpressionType.Greater:
                    _result = WithCommonNumericType(left, right, L.Expression.GreaterThan, expression.Type);
                    break;
                case BinaryExpressionType.Equal:
                    _result = WithCommonNumericType(left, right, L.Expression.Equal, expression.Type);
                    break;
                case BinaryExpressionType.Minus:
                    if (Checked) _result = WithCommonNumericType(left, right, L.Expression.SubtractChecked);
                    else _result = WithCommonNumericType(left, right, L.Expression.Subtract);
                    break;
                case BinaryExpressionType.Plus:
                    if (Checked) _result = WithCommonNumericType(left, right, L.Expression.AddChecked);
                    else _result = WithCommonNumericType(left, right, L.Expression.Add);
                    break;
                case BinaryExpressionType.Modulo:
                    _result = WithCommonNumericType(left, right, L.Expression.Modulo);
                    break;
                case BinaryExpressionType.Div:
                    _result = WithCommonNumericType(left, right, L.Expression.Divide);
                    break;
                case BinaryExpressionType.Times:
                    if (Checked) _result = WithCommonNumericType(left, right, L.Expression.MultiplyChecked);
                    else _result = WithCommonNumericType(left, right, L.Expression.Multiply);
                    break;
                case BinaryExpressionType.BitwiseOr:
                    _result = L.Expression.Or(left, right);
                    break;
                case BinaryExpressionType.BitwiseAnd:
                    _result = L.Expression.And(left, right);
                    break;
                case BinaryExpressionType.BitwiseXOr:
                    _result = L.Expression.ExclusiveOr(left, right);
                    break;
                case BinaryExpressionType.LeftShift:
                    _result = L.Expression.LeftShift(left, right);
                    break;
                case BinaryExpressionType.RightShift:
                    _result = L.Expression.RightShift(left, right);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void Visit(UnaryExpression expression)
        {
            expression.Expression.Accept(this);
            switch (expression.Type)
            {
                case UnaryExpressionType.Not:
                    _result = L.Expression.Not(_result);
                    break;
                case UnaryExpressionType.Negate:
                    _result = L.Expression.Negate(_result);
                    break;
                case UnaryExpressionType.BitwiseNot:
                    _result = L.Expression.Not(_result);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void Visit(ValueExpression expression)
        {
            _result = L.Expression.Constant(expression.Value);
        }

        public override void Visit(Function function)
        {
            var args = new L.Expression[function.Expressions.Length];
            for (int i = 0; i < function.Expressions.Length; i++)
            {
                function.Expressions[i].Accept(this);
                args[i] = _result;
            }

            switch (function.Identifier.Name.ToLowerInvariant())
            {
                case "if":
                    _result = L.Expression.Condition(args[0], args[1], args[2]);
                    break;
                case "in":
                    var items = L.Expression.NewArrayInit(args[0].Type,
                        new ArraySegment<L.Expression>(args, 1, args.Length - 1));
                    var smi = typeof (Array).GetRuntimeMethod("IndexOf", new[] { typeof(Array), typeof(object) });
                    var r = L.Expression.Call(smi, L.Expression.Convert(items, typeof(Array)), L.Expression.Convert(args[0], typeof(object)));
                    _result = L.Expression.GreaterThanOrEqual(r, L.Expression.Constant(0));
                    break;
                default:
                    var mi = FindMethod(function.Identifier.Name, args);
                    _result = L.Expression.Call(_context, mi.BaseMethodInfo, mi.PreparedArguments);
                    break;
            }
        }

        public override void Visit(Identifier function)
        {
            if (_context == null)
            {
                _result = L.Expression.Constant(_parameters[function.Name]);
            }
            else
            {
                _result = L.Expression.PropertyOrField(_context, function.Name);
            }
        }

        private ExtendedMethodInfo FindMethod(string methodName, L.Expression[] methodArgs) 
        {
            var methods = _context.Type.GetTypeInfo().DeclaredMethods.Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) && m.IsPublic && !m.IsStatic);
            foreach (var potentialMethod in methods) 
            {
                var methodParams = potentialMethod.GetParameters();
                var newArguments = PrepareMethodArgumentsIfValid(methodParams, methodArgs);

                if (newArguments != null) 
                {
                    return new ExtendedMethodInfo() { BaseMethodInfo = potentialMethod, PreparedArguments = newArguments };
                }
            }

            throw new MissingMethodException($"method not found: {methodName}");
        }

        private L.Expression[] PrepareMethodArgumentsIfValid(ParameterInfo[] parameters, L.Expression[] arguments) 
        {
            if (!parameters.Any() && !arguments.Any()) return arguments;
            if (!parameters.Any()) return null;
            bool paramsMatchArguments = true;

            var lastParameter = parameters.Last();
            bool hasParamsKeyword = lastParameter.IsDefined(typeof(ParamArrayAttribute));
            if (hasParamsKeyword && parameters.Length > arguments.Length) return null;
            L.Expression[] newArguments = new L.Expression[parameters.Length];
            L.Expression[] paramsKeywordArgument = null;
            Type paramsElementType = null;
            int paramsParameterPosition = 0;
            if (!hasParamsKeyword) 
            {
                paramsMatchArguments &= parameters.Length == arguments.Length;
                if (!paramsMatchArguments) return null;
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
                var argumentType = arguments[i].Type;
                var parameterType = isParamsElement ? paramsElementType : parameters[i].ParameterType;
                paramsMatchArguments &= argumentType == parameterType;
                if (!paramsMatchArguments) return null;
                if (!isParamsElement) 
                {
                    newArguments[i] = arguments[i];
                } 
                else 
                {
                    paramsKeywordArgument[i - paramsParameterPosition] = arguments[i];
                }
            }

            if (hasParamsKeyword) 
            {
                newArguments[paramsParameterPosition] = L.Expression.NewArrayInit(paramsElementType, paramsKeywordArgument);
            }
            return newArguments;
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
