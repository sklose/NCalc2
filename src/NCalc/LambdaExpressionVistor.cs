using System;
using System.Reflection;
using NCalc.Domain;
using L = System.Linq.Expressions;
using ValueType = NCalc.Domain.ValueType;

namespace NCalc
{
    internal class LambdaExpressionVistor : LogicalExpressionVisitor
    {
        private L.Expression _result;
        private readonly L.Expression _parameter;

        public LambdaExpressionVistor(L.ParameterExpression parameter)
        {
            _parameter = parameter;
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
                    _result = L.Expression.NotEqual(left, right);
                    break;
                case BinaryExpressionType.LesserOrEqual:
                    _result = L.Expression.LessThanOrEqual(left, right);
                    break;
                case BinaryExpressionType.GreaterOrEqual:
                    _result = L.Expression.GreaterThanOrEqual(left, right);
                    break;
                case BinaryExpressionType.Lesser:
                    _result = L.Expression.LessThan(left, right);
                    break;
                case BinaryExpressionType.Greater:
                    _result = L.Expression.GreaterThan(left, right);
                    break;
                case BinaryExpressionType.Equal:
                    _result = L.Expression.Equal(left, right);
                    break;
                case BinaryExpressionType.Minus:
                    _result = L.Expression.Subtract(left, right);
                    break;
                case BinaryExpressionType.Plus:
                    _result = L.Expression.Add(left, right);
                    break;
                case BinaryExpressionType.Modulo:
                    _result = L.Expression.Modulo(left, right);
                    break;
                case BinaryExpressionType.Div:
                    _result = L.Expression.Divide(left, right);
                    break;
                case BinaryExpressionType.Times:
                    _result = L.Expression.Multiply(left, right);
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
            switch (expression.Type)
            {
                case ValueType.Integer:
                    _result = L.Expression.Constant(expression.Value, typeof(int));
                    break;
                case ValueType.String:
                    _result = L.Expression.Constant(expression.Value, typeof(string));
                    break;
                case ValueType.DateTime:
                    _result = L.Expression.Constant(expression.Value, typeof(DateTime));
                    break;
                case ValueType.Float:
                    _result = L.Expression.Constant(expression.Value, typeof(float));
                    break;
                case ValueType.Boolean:
                    _result = L.Expression.Constant(expression.Value, typeof(bool));
                    break;
            }
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
                    var smi = typeof (Array).GetMethod("IndexOf", new[] { typeof(Array), typeof(object) });
                    var r = L.Expression.Call(smi, L.Expression.Convert(items, typeof(Array)), L.Expression.Convert(args[0], typeof(object)));
                    _result = L.Expression.GreaterThanOrEqual(r, L.Expression.Constant(0));
                    break;
                default:
                    var mi = _parameter.Type.GetMethod(function.Identifier.Name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    _result = L.Expression.Call(_parameter, mi, args);
                    break;
            }
        }

        public override void Visit(Identifier function)
        {
            _result = L.Expression.PropertyOrField(_parameter, function.Name);
        }
    }
}
