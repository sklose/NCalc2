using System.Globalization;

namespace NCalc.Tests
{
    internal static class Extensions
    {
        internal static Expression CreateExpression(string expression, CultureInfo? cultureInfo = null) =>
           new Expression(expression, cultureInfo ?? CultureInfo.InvariantCulture);

        internal static Expression CreateExpression(string expression, EvaluateOptions evaluateOptions, CultureInfo? cultureInfo = null) =>
           new Expression(expression, evaluateOptions, cultureInfo ?? CultureInfo.InvariantCulture);
    }
}
