using NCalc.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using L = System.Linq.Expressions;

namespace NCalc
{
    internal class LambdaExpressionVistor : LogicalExpressionVisitor
    {
        private readonly IDictionary<string, object> _parameters;
        private L.Expression _result;
        private readonly L.Expression _context;
        private readonly EvaluateOptions _options = EvaluateOptions.None;
        private readonly Dictionary<Type, HashSet<Type>> _implicitPrimitiveConversionTable = new Dictionary<Type, HashSet<Type>>() {
            { typeof(sbyte), new HashSet<Type> { typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(byte), new HashSet<Type> { typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(short), new HashSet<Type> { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(ushort), new HashSet<Type> { typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(int), new HashSet<Type> { typeof(long), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(uint), new HashSet<Type> { typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(long), new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) }},
            { typeof(char), new HashSet<Type> { typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(float), new HashSet<Type> { typeof(double) }},
            { typeof(ulong), new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) }},
        };

        private struct MathCallFunction
        {
            public MethodInfo MathMethodInfo;
            public int ArgumentCount;
        }

        private static MathCallFunction GetMathCallFunctionHelper(string method, int argCount) => new MathCallFunction {
            MathMethodInfo = typeof(Math).GetMethod(method, Enumerable.Repeat(typeof(double), argCount).ToArray()),
            ArgumentCount = argCount };

        private static readonly Dictionary<string, MathCallFunction> _mathCallFunctions = new Dictionary<string, MathCallFunction>()
        {
            { "ABS",           GetMathCallFunctionHelper(nameof(Math.Abs),           1 ) },
            { "ACOS",          GetMathCallFunctionHelper(nameof(Math.Acos),          1 ) },
            { "ASIN",          GetMathCallFunctionHelper(nameof(Math.Asin),          1 ) },
            { "ATAN",          GetMathCallFunctionHelper(nameof(Math.Atan),          1 ) },
            { "ATAN2",         GetMathCallFunctionHelper(nameof(Math.Atan2),         2 ) },
            { "CEILING",       GetMathCallFunctionHelper(nameof(Math.Ceiling),       1 ) },
            { "COS",           GetMathCallFunctionHelper(nameof(Math.Cos),           1 ) },
            { "COSH",          GetMathCallFunctionHelper(nameof(Math.Cosh),          1 ) },
            { "EXP",           GetMathCallFunctionHelper(nameof(Math.Exp),           1 ) },
            { "FLOOR",         GetMathCallFunctionHelper(nameof(Math.Floor),         1 ) },
            { "IEEEREMAINDER", GetMathCallFunctionHelper(nameof(Math.IEEERemainder), 2 ) },
            { "LOG",           GetMathCallFunctionHelper(nameof(Math.Log),           2 ) },
            { "LOG10",         GetMathCallFunctionHelper(nameof(Math.Log10),         1 ) },
            { "SIGN",          GetMathCallFunctionHelper(nameof(Math.Sign),          1 ) },
            { "SIN",           GetMathCallFunctionHelper(nameof(Math.Sin),           1 ) },
            { "SINH",          GetMathCallFunctionHelper(nameof(Math.Sinh),          1 ) },
            { "SQRT",          GetMathCallFunctionHelper(nameof(Math.Sqrt),          1 ) },
            { "TAN",           GetMathCallFunctionHelper(nameof(Math.Tan),           1 ) },
            { "TANH",          GetMathCallFunctionHelper(nameof(Math.Tanh),          1 ) },
            { "TRUNCATE",      GetMathCallFunctionHelper(nameof(Math.Truncate),      1 ) },

            // Exceptional handling
            { "ROUND",         new MathCallFunction() { 
                MathMethodInfo = typeof(Math).GetMethod(nameof(Math.Round), new[] { typeof(double), typeof(int), typeof(MidpointRounding) }),
                ArgumentCount = 2 } }
        };

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

            string functionName = function.Identifier.Name.ToUpperInvariant();
            if (functionName == "IF")
            {
                var numberTypePriority = new Type[] { typeof(double), typeof(float), typeof(long), typeof(int), typeof(short) };
                var index1 = Array.IndexOf(numberTypePriority, args[1].Type);
                var index2 = Array.IndexOf(numberTypePriority, args[2].Type);
                if (index1 >= 0 && index2 >= 0 && index1 != index2)
                {
                    args[1] = L.Expression.Convert(args[1], numberTypePriority[Math.Min(index1, index2)]);
                    args[2] = L.Expression.Convert(args[2], numberTypePriority[Math.Min(index1, index2)]);
                }
                _result = L.Expression.Condition(args[0], args[1], args[2]);
                return;
            }
            else if (functionName == "IN")
            {
                var items = L.Expression.NewArrayInit(args[0].Type,
                        new ArraySegment<L.Expression>(args, 1, args.Length - 1));
                var smi = typeof(Array).GetRuntimeMethod("IndexOf", new[] { typeof(Array), typeof(object) });
                var r = L.Expression.Call(smi, L.Expression.Convert(items, typeof(Array)), L.Expression.Convert(args[0], typeof(object)));
                _result = L.Expression.GreaterThanOrEqual(r, L.Expression.Constant(0));
                return;
            }

            //Context methods take precedence over built-in functions because they're user-customisable.
            var mi = FindMethod(function.Identifier.Name, args);
            if (mi != null)
            {
                _result = L.Expression.Call(_context, mi.BaseMethodInfo, mi.PreparedArguments);
                return;
            }

            void CheckArgumentsLengthForFunction(string funcStr, int argsNum, int argsNeed)
            {
                if (argsNum != argsNeed)
                    throw new ArgumentException($"{funcStr} takes exactly {argsNeed} argument");
            };

            void MakeMathCallExpression(MathCallFunction mathMethod, int argsNumActual)
            {
                CheckArgumentsLengthForFunction(mathMethod.MathMethodInfo.Name, argsNumActual, mathMethod.ArgumentCount);

                _result = L.Expression.Call(mathMethod.MathMethodInfo,
                    Enumerable.Range(0, argsNumActual).Select( i => L.Expression.Convert(args[i], typeof(double)) ));
            }

            L.UnaryExpression arg0;
            L.UnaryExpression arg1;

            int actualNumArgs = function.Expressions.Length;

            switch (functionName)
            {
                // Exceptional handling
                case "MAX":
                    CheckArgumentsLengthForFunction(functionName, function.Expressions.Length, 2);
                    arg0 = L.Expression.Convert(args[0], typeof(double));
                    arg1 = L.Expression.Convert(args[1], typeof(double));
                    _result = L.Expression.Condition(L.Expression.GreaterThan(arg0, arg1), arg0, arg1);
                    break;
                case "MIN":
                    CheckArgumentsLengthForFunction(functionName, function.Expressions.Length, 2);
                    arg0 = L.Expression.Convert(args[0], typeof(double));
                    arg1 = L.Expression.Convert(args[1], typeof(double));
                    _result = L.Expression.Condition(L.Expression.LessThan(arg0, arg1), arg0, arg1);
                    break;
                case "POW":
                    CheckArgumentsLengthForFunction(functionName, function.Expressions.Length, 2);
                    arg0 = L.Expression.Convert(args[0], typeof(double));
                    arg1 = L.Expression.Convert(args[1], typeof(double));
                    _result = L.Expression.Power(arg0, arg1);
                    break;
                case "ROUND":
                    CheckArgumentsLengthForFunction(functionName, function.Expressions.Length, 2);
                    arg0 = L.Expression.Convert(args[0], typeof(double));
                    arg1 = L.Expression.Convert(args[1], typeof(int));
                    MidpointRounding rounding = (_options & EvaluateOptions.RoundAwayFromZero) == EvaluateOptions.RoundAwayFromZero ? MidpointRounding.AwayFromZero : MidpointRounding.ToEven;
                    _result = L.Expression.Call(_mathCallFunctions["ROUND"].MathMethodInfo, arg0, arg1, L.Expression.Constant(rounding));
                    break;
                default:
                    // Regular handling
                    if (_mathCallFunctions.TryGetValue(functionName, out MathCallFunction func))
                    {
                        MakeMathCallExpression(func, actualNumArgs);
                    }
                    else
                    {
                        throw new MissingMethodException($"method not found: {functionName}");
                    }
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
            if (_context == null) return null;

            TypeInfo contextTypeInfo = _context.Type.GetTypeInfo();
            TypeInfo objectTypeInfo = typeof(object).GetTypeInfo();
            do
            {
                var methods = contextTypeInfo.DeclaredMethods.Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) && m.IsPublic && !m.IsStatic);
                var candidates = new List<ExtendedMethodInfo>();
                foreach (var potentialMethod in methods)
                {
                    var methodParams = potentialMethod.GetParameters();
                    var preparedArguments = PrepareMethodArgumentsIfValid(methodParams, methodArgs);

                    if (preparedArguments != null)
                    {
                        var candidate = new ExtendedMethodInfo()
                        {
                            BaseMethodInfo = potentialMethod,
                            PreparedArguments = preparedArguments.Item2,
                            Score = preparedArguments.Item1
                        };
                        if (candidate.Score == 0) return candidate;
                        candidates.Add(candidate);
                    }
                }
                if (candidates.Count != 0) return candidates.OrderBy(method => method.Score).First();
                contextTypeInfo = contextTypeInfo.BaseType.GetTypeInfo();
            } while (contextTypeInfo != objectTypeInfo);
            return null;
        }

        /// <summary>
        /// Returns a tuple where the first item is a score, and the second is a list of prepared arguments. 
        /// Score is a simplified indicator of how close the arguments' types are to the parameters'. A score of 0 indicates a perfect match between arguments and parameters. 
        /// Prepared arguments refers to having the arguments implicitly converted where necessary, and "params" arguments collated into one array.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private Tuple<int, L.Expression[]> PrepareMethodArgumentsIfValid(ParameterInfo[] parameters, L.Expression[] arguments)
        {
            if (parameters.Length == 0 && arguments.Length == 0) return Tuple.Create(0, arguments);
            if (parameters.Length == 0) return null;

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

            int functionMemberScore = 0;
            for (int i = 0; i < arguments.Length; i++)
            {
                var isParamsElement = hasParamsKeyword && i >= paramsParameterPosition;
                var argument = arguments[i];
                var argumentType = argument.Type;
                var parameterType = isParamsElement ? paramsElementType : parameters[i].ParameterType;
                if (argumentType != parameterType)
                {
                    bool canCastImplicitly = TryCastImplicitly(argumentType, parameterType, ref argument);
                    if (!canCastImplicitly) return null;
                    functionMemberScore++;
                }
                if (!isParamsElement)
                {
                    newArguments[i] = argument;
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
            return Tuple.Create(functionMemberScore, newArguments);
        }

        private bool TryCastImplicitly(Type from, Type to, ref L.Expression argument)
        {
            bool convertingFromPrimitiveType = _implicitPrimitiveConversionTable.TryGetValue(from, out var possibleConversions);
            if (!convertingFromPrimitiveType || !possibleConversions.Contains(to))
            {
                argument = null;
                return false;
            }
            argument = L.Expression.Convert(argument, to);
            return true;
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

        private static L.Expression UnwrapNullable(L.Expression expression)
        {
            var ti = expression.Type.GetTypeInfo();
            if (ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(Nullable<>))
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
