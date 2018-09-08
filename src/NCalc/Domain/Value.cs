using System;
using System.Reflection;
using System.Threading.Tasks;

namespace NCalc.Domain
{
    public class ValueExpression : LogicalExpression
    {
        public ValueExpression(object value, ValueType type)
        {
            Value = value;
            Type = type;
        }

        public ValueExpression(object value)
        {
            switch (value.GetTypeCode())
            {
                case TypeCode.Boolean:
                    Type = ValueType.Boolean;
                    break;

                case TypeCode.DateTime:
                    Type = ValueType.DateTime;
                    break;

                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    Type = ValueType.Float;
                    break;

                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    Type = ValueType.Integer;
                    break;

                case TypeCode.String:
                    Type = ValueType.String;
                    break;

                default:
                    throw new EvaluationException("This value could not be handled: " + value);
            }

            Value = value;
        }

        public ValueExpression(string value)
        {
            Value = value;
            Type = ValueType.String;
        }

        public ValueExpression(int value)
        {
            Value = value;
            Type = ValueType.Integer;
        }

        public ValueExpression(float value)
        {
            Value = value;
            Type = ValueType.Float;
        }

        public ValueExpression(DateTime value)
        {
            Value = value;
            Type = ValueType.DateTime;
        }

        public ValueExpression(bool value)
        {
            Value = value;
            Type = ValueType.Boolean;
        }

        public object Value { get; set; }
        public ValueType Type { get; set; }

        public override void Accept(LogicalExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public async override Task AcceptAsync(LogicalExpressionVisitor visitor)
        {
            await visitor.VisitAsync(this);
        }

        public static object GetUnderlyingValue(System.Linq.Expressions.Expression e)
        {

            if (e.Type == typeof(Boolean)) { return Boolean.Parse(e.ToString()); }
            else if (e.Type == typeof(DateTime)) { return DateTime.Parse(e.ToString()); }
            else if (e.Type == typeof(Decimal)) { return Decimal.Parse(e.ToString()); }
            else if (e.Type == typeof(Double)) { return Double.Parse(e.ToString()); }
            else if (e.Type == typeof(Single)) { return Single.Parse(e.ToString()); }
            else if (e.Type == typeof(Byte)) { return Byte.Parse(e.ToString()); }
            else if (e.Type == typeof(SByte)) { return SByte.Parse(e.ToString()); }
            else if (e.Type == typeof(Int16)) { return Int16.Parse(e.ToString()); }
            else if (e.Type == typeof(Int32)) { return Int32.Parse(e.ToString()); }
            else if (e.Type == typeof(Int64)) { return Int64.Parse(e.ToString()); }
            else if (e.Type == typeof(UInt16)) { return UInt16.Parse(e.ToString()); }
            else if (e.Type == typeof(UInt32)) { return UInt32.Parse(e.ToString()); }
            else if (e.Type == typeof(UInt64)) { return UInt64.Parse(e.ToString()); }
            else if (e.Type == typeof(String)) { return e.ToString(); }
            else
                return string.Empty;

        }
    }

    public enum ValueType
    {
        Integer,
        String,
        DateTime,
        Float,
        Boolean
    }
}
