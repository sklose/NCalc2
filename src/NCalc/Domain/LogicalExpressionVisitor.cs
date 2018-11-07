
using System.Threading.Tasks;

namespace NCalc.Domain
{
    public abstract class LogicalExpressionVisitor
    {
        public abstract void Visit(LogicalExpression expression);
        public abstract Task VisitAsync(LogicalExpression expression);
        public abstract void Visit(TernaryExpression expression);
        public abstract Task VisitAsync(TernaryExpression expression);
        public abstract void Visit(BinaryExpression expression);
        public abstract Task VisitAsync(BinaryExpression expression);
        public abstract void Visit(UnaryExpression expression);
        public abstract Task VisitAsync(UnaryExpression expression);
	    public abstract void Visit(ValueExpression expression);
        public abstract Task VisitAsync(ValueExpression expression);
        public abstract void Visit(Function function);
        public abstract Task VisitAsync(Function function);
        public abstract void Visit(Identifier function);
        public abstract Task VisitAsync(Identifier function);
    }
}
