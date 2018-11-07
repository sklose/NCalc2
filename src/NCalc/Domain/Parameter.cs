using System.Threading.Tasks;

namespace NCalc.Domain
{
	public class Identifier : LogicalExpression
	{
		public Identifier(string name)
		{
            Name = name;
		}

	    public string Name { get; set; }


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
