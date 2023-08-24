using Antlr4.Runtime;

namespace NCalc
{
    internal class SyntaxError<T>
    {
        public T OffendingSymbol;
        public int Line;
        public int CharPositionInLine;
        public string Message;
        public RecognitionException Exception;

        public SyntaxError(T offendingSymbol, int line, int charPositionInLine, string message, RecognitionException exception)
        {
            OffendingSymbol = offendingSymbol;
            Line = line;
            CharPositionInLine = charPositionInLine;
            Message = message;
            Exception = exception;
        }

        public override string ToString() => $"{Message}:{Line}:{CharPositionInLine}";
    }
}
