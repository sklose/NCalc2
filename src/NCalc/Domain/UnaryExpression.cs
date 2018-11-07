using System.Threading.Tasks;

namespace NCalc.Domain
{
	public class UnaryExpression : LogicalExpression
    {
		public UnaryExpression(UnaryExpressionType type, LogicalExpression expression)
		{
            Type = type;
            Expression = expression;
		}

	    public LogicalExpression Expression { get; set; }

	    public UnaryExpressionType Type { get; set; }

	    public override void Accept(LogicalExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public async override Task AcceptAsync(LogicalExpressionVisitor visitor)
        {
            await visitor.VisitAsync(this);
        }
    }

	public enum UnaryExpressionType
	{
		Not,
        Negate,
        BitwiseNot
	}
}
