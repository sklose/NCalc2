using System;

namespace NCalc
{
    [Serializable]
    public class EvaluationException : Exception
    {
        public EvaluationException(string message)
            : base(message)
        {
        }

        public EvaluationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

    }
}
