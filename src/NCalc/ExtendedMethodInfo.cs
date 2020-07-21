using System.Reflection;
using L = FastExpressionCompiler.LightExpression;

namespace NCalc 
{
    public class ExtendedMethodInfo 
    {
        public MethodInfo BaseMethodInfo { get; set; }
        public L.Expression[] PreparedArguments { get; set; }

    }
}
