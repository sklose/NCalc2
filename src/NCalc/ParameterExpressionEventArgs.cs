using System;
using LQ = System.Linq.Expressions;

namespace NCalc
{
    public class ParameterExpressionEventArgs : EventArgs
    {
        public ParameterExpressionEventArgs(string name)
        {
            this.Name = name;
        }

        public string Name { get; }

        public LQ.Expression Result { get; set; }

        public bool HasResult => Result != null;
    }
}