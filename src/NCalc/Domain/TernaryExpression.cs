using System.Threading.Tasks;

namespace NCalc.Domain
{
	public class TernaryExpression : LogicalExpression
	{
        public TernaryExpression(LogicalExpression leftExpression, LogicalExpression middleExpression, LogicalExpression rightExpression)
		{
            this.LeftExpression = leftExpression;
            this.MiddleExpression = middleExpression;
            this.RightExpression = rightExpression;
		}

	    public LogicalExpression LeftExpression { get; set; }

	    public LogicalExpression MiddleExpression { get; set; }

	    public LogicalExpression RightExpression { get; set; }

	    public override void Accept(LogicalExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public async override Task AcceptAsync(LogicalExpressionVisitor visitor)
        {
            await visitor.VisitAsync(this);
        }

    }   
}