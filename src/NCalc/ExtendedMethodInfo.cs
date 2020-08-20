using System.Reflection;
using L = System.Linq.Expressions;

namespace NCalc 
{
    public class ExtendedMethodInfo 
    {
        public MethodInfo BaseMethodInfo { get; set; }
        public L.Expression[] PreparedArguments { get; set; }
        public int Score { get; set; }
    }
}