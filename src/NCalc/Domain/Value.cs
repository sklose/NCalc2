using System;
using System.Reflection;

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
                    Value = value;

                    break;

                case TypeCode.DateTime:
                    Type = ValueType.DateTime;
                    Value = value;

                    break;

                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    Type = ValueType.Float;
                    Value = Convert.ToDecimal(value);

                    break;

                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    Type = ValueType.Integer;
                    Value = Convert.ToInt64(value);

                    break;
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    Type = ValueType.Integer;
                    Value = value;

                    break;

                case TypeCode.String:
                    Type = ValueType.String;
                    Value = value;

                    break;

                default:
                    throw new EvaluationException(
                        "This value could not be handled: " + value);
            }
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
