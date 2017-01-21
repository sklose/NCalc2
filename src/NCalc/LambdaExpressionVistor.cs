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

        public LambdaExpressionVistor(IDictionary<string, object> parameters)
        {
            _parameters = parameters;
        }

        public LambdaExpressionVistor(L.ParameterExpression context)
        {
            _context = context;
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
                    _result = WithCommonNumericType(left, right, L.Expression.NotEqual);
                    break;
                case BinaryExpressionType.LesserOrEqual:
                    _result = WithCommonNumericType(left, right, L.Expression.LessThanOrEqual);
                    break;
                case BinaryExpressionType.GreaterOrEqual:
                    _result = WithCommonNumericType(left, right, L.Expression.GreaterThanOrEqual);
                    break;
                case BinaryExpressionType.Lesser:
                    _result = WithCommonNumericType(left, right, L.Expression.LessThan);
                    break;
                case BinaryExpressionType.Greater:
                    _result = WithCommonNumericType(left, right, L.Expression.GreaterThan);
                    break;
                case BinaryExpressionType.Equal:
                    _result = WithCommonNumericType(left, right, L.Expression.Equal);
                    break;
                case BinaryExpressionType.Minus:
                    _result = WithCommonNumericType(left, right, L.Expression.Subtract);
                    break;
                case BinaryExpressionType.Plus:
                    _result = WithCommonNumericType(left, right, L.Expression.Add);
                    break;
                case BinaryExpressionType.Modulo:
                    _result = WithCommonNumericType(left, right, L.Expression.Modulo);
                    break;
                case BinaryExpressionType.Div:
                    _result = WithCommonNumericType(left, right, L.Expression.Divide);
                    break;
                case BinaryExpressionType.Times:
                    _result = WithCommonNumericType(left, right, L.Expression.Multiply);
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
                    var mi = _context.Type.GetTypeInfo().DeclaredMethods.FirstOrDefault(
                        m => m.Name.Equals(function.Identifier.Name, StringComparison.OrdinalIgnoreCase) &&
                             m.IsPublic && !m.IsStatic);
                    _result = L.Expression.Call(_context, mi, args);
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

        private L.Expression WithCommonNumericType(L.Expression left, L.Expression right,
            Func<L.Expression, L.Expression, L.Expression> action)
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
