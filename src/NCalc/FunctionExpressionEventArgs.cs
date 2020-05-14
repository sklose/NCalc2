using System;
using LQ = System.Linq.Expressions;

namespace NCalc
{
    public class FunctionExpressionEventArgs : EventArgs
    {
        public FunctionExpressionEventArgs(string name, LQ.Expression[] argumentExpressions)
        {
            Name = name;
            ArgumentExpressions = argumentExpressions;
        }

        public string Name { get; }

        public LQ.Expression[] ArgumentExpressions { get; }

        public LQ.Expression Result { get; set; }

        public bool HasResult => Result != null;
    }
}