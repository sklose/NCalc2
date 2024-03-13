using System;
using System.Collections.Generic;
using System.Globalization;

namespace NCalc.Domain
{
    public class EvaluationVisitor : LogicalExpressionVisitor
    {
        private delegate T Func<T>();

        private readonly EvaluateOptions _options = EvaluateOptions.None;
        private readonly CultureInfo _cultureInfo = CultureInfo.CurrentCulture;
        private readonly StringComparer _comparer;

        private bool IgnoreCase { get { return (_options & EvaluateOptions.IgnoreCase) == EvaluateOptions.IgnoreCase; } }
        private bool Ordinal { get { return (_options & EvaluateOptions.MatchStringsOrdinal) == EvaluateOptions.MatchStringsOrdinal; } }
        private bool IgnoreCaseString { get { return (_options & EvaluateOptions.MatchStringsWithIgnoreCase) == EvaluateOptions.MatchStringsWithIgnoreCase; } }
        private bool Checked { get { return (_options & EvaluateOptions.OverflowProtection) == EvaluateOptions.OverflowProtection; } }

        public EvaluationVisitor(EvaluateOptions options) : this(options, CultureInfo.CurrentCulture) { }

        public EvaluationVisitor(EvaluateOptions options, CultureInfo cultureInfo)
        {
            _options = options;
            _cultureInfo = cultureInfo;

            if (Ordinal)
                _comparer = IgnoreCaseString ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            else
                _comparer = StringComparer.Create(_cultureInfo, IgnoreCaseString);
        }

        public object Result { get; protected set; }

        private object Evaluate(LogicalExpression expression)
        {
            expression.Accept(this);
            return Result;
        }

        public override void Visit(LogicalExpression expression)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private static Type[] CommonTypes = new[] { typeof(Int64), typeof(Double), typeof(Boolean), typeof(String), typeof(Decimal) };

        /// <summary>
        /// Gets the the most precise type.
        /// </summary>
        /// <param name="a">Type a.</param>
        /// <param name="b">Type b.</param>
        /// <returns></returns>
        private static Type GetMostPreciseType(Type a, Type b)
        {
            foreach (Type t in CommonTypes)
            {
                if (a == t || b == t)
                {
                    return t;
                }
            }

            return a ?? b;
        }

        public int CompareUsingMostPreciseType(object a, object b)
        {
            var allowNull = (_options & EvaluateOptions.AllowNullParameter) == EvaluateOptions.AllowNullParameter;

            Type mpt = allowNull ? GetMostPreciseType(a?.GetType(), b?.GetType()) ?? typeof(object) : GetMostPreciseType(a.GetType(), b.GetType());

            if (a == null && b == null)
            {
                return 0;
            }

            if (a == null || b == null)
            {
                return -1;
            }

            a = Convert.ChangeType(a, mpt, _cultureInfo);
            b = Convert.ChangeType(b, mpt, _cultureInfo);

            if (mpt.Equals(typeof(string)) && (Ordinal || IgnoreCaseString))
                return _comparer.Compare(a?.ToString(), b?.ToString());

            if (ReferenceEquals(a, b))
            {
                return 0;
            }

            var cmp = a as IComparable;
            if (cmp != null)
                return cmp.CompareTo(b);

            return -1;
            //return Comparer.Default.Compare(Convert.ChangeType(a, mpt), Convert.ChangeType(b, mpt));
        }

        public override void Visit(TernaryExpression expression)
        {
            // Evaluates the left expression and saves the value
            expression.LeftExpression.Accept(this);
            bool left = Convert.ToBoolean(Result, _cultureInfo);

            if (left)
            {
                expression.MiddleExpression.Accept(this);
            }
            else
            {
                expression.RightExpression.Accept(this);
            }
        }

        private static bool IsReal(object value)
        {
            var typeCode = value.GetTypeCode();
            return typeCode == TypeCode.Decimal || typeCode == TypeCode.Double || typeCode == TypeCode.Single;
        }

        public override void Visit(BinaryExpression expression)
        {
            // simulate Lazy<Func<>> behavior for late evaluation
            object leftValue = null;
            bool leftEvaluated = false;
            Func<object> left = () =>
                                 {
                                     if (!leftEvaluated)
                                     {
                                         expression.LeftExpression.Accept(this);
                                         leftValue = Result;
                                         leftEvaluated = true;
                                     }
                                     return leftValue;
                                 };

            // simulate Lazy<Func<>> behavior for late evaluation
            object rightValue = null;
            bool rightEvaluated = false;
            Func<object> right = () =>
            {
                if (!rightEvaluated)
                {
                    expression.RightExpression.Accept(this);
                    rightValue = Result;
                    rightEvaluated = true;
                }
                return rightValue;
            };

            switch (expression.Type)
            {
                case BinaryExpressionType.And:
                    Result = Convert.ToBoolean(left(), _cultureInfo) && Convert.ToBoolean(right(), _cultureInfo);
                    break;

                case BinaryExpressionType.Or:
                    Result = Convert.ToBoolean(left(), _cultureInfo) || Convert.ToBoolean(right(), _cultureInfo);
                    break;

                case BinaryExpressionType.Div:
                    //Actually doesn't need checked here, since if one is real,
                    // checked does nothing, and if they are int the result will only be same or smaller
                    // (since anything between 1 and 0 is not int and 0 is an exception anyway
                    Result = IsReal(left()) || IsReal(right())
                                 ? Numbers.Divide(left(), right(), _options, _cultureInfo)
                                 : Numbers.Divide(Convert.ToDouble(left(), _cultureInfo), right(), _options, _cultureInfo);
                    break;

                case BinaryExpressionType.Equal:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(left(), right()) == 0;
                    break;

                case BinaryExpressionType.Greater:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(left(), right()) > 0;
                    break;

                case BinaryExpressionType.GreaterOrEqual:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(left(), right()) >= 0;
                    break;

                case BinaryExpressionType.Lesser:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(left(), right()) < 0;
                    break;

                case BinaryExpressionType.LesserOrEqual:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(left(), right()) <= 0;
                    break;

                case BinaryExpressionType.Minus:
                    Result = Checked
                        ? Numbers.SubtractChecked(left(), right(), _options, _cultureInfo)
                        : Numbers.Subtract(left(), right(), _options, _cultureInfo);
                    break;


                case BinaryExpressionType.Modulo:
                    Result = Numbers.Modulo(left(), right(), _cultureInfo);
                    break;

                case BinaryExpressionType.NotEqual:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(left(), right()) != 0;
                    break;

                case BinaryExpressionType.Plus:
                    if (left() is string)
                    {
                        Result = String.Concat(left(), right());
                    }
                    else
                    {
                        Result = Checked
                            ? Numbers.AddChecked(left(), right(), _options, _cultureInfo)
                            : Numbers.Add(left(), right(), _options, _cultureInfo);
                    }

                    break;

                case BinaryExpressionType.Times:
                    Result = Checked
                        ? Numbers.MultiplyChecked(left(), right(), _options, _cultureInfo)
                        : Numbers.Multiply(left(), right(), _options, _cultureInfo);
                    break;

                case BinaryExpressionType.BitwiseAnd:
                    Result = Convert.ToUInt16(left(), _cultureInfo) & Convert.ToUInt16(right(), _cultureInfo);
                    break;


                case BinaryExpressionType.BitwiseOr:
                    Result = Convert.ToUInt16(left(), _cultureInfo) | Convert.ToUInt16(right(), _cultureInfo);
                    break;


                case BinaryExpressionType.BitwiseXOr:
                    Result = Convert.ToUInt16(left(), _cultureInfo) ^ Convert.ToUInt16(right(), _cultureInfo);
                    break;


                case BinaryExpressionType.LeftShift:
                    Result = Convert.ToUInt16(left(), _cultureInfo) << Convert.ToUInt16(right(), _cultureInfo);
                    break;


                case BinaryExpressionType.RightShift:
                    Result = Convert.ToUInt16(left(), _cultureInfo) >> Convert.ToUInt16(right(), _cultureInfo);
                    break;
            }
        }

        public override void Visit(UnaryExpression expression)
        {
            // Recursively evaluates the underlying expression
            expression.Expression.Accept(this);

            switch (expression.Type)
            {
                case UnaryExpressionType.Not:
                    Result = !Convert.ToBoolean(Result, _cultureInfo);
                    break;

                case UnaryExpressionType.Negate:
                    Result = Numbers.Subtract(0, Result, _options, _cultureInfo);
                    break;

                case UnaryExpressionType.BitwiseNot:
                    Result = ~Convert.ToUInt16(Result, _cultureInfo);
                    break;
            }
        }

        public override void Visit(ValueExpression expression)
        {
            Result = expression.Value;
        }

        public override void Visit(Function function)
        {
            var args = new FunctionArgs
            {
                Parameters = new Expression[function.Expressions.Length]
            };

            // Don't call parameters right now, instead let the function do it as needed.
            // Some parameters shouldn't be called, for instance, in a if(), the "not" value might be a division by zero
            // Evaluating every value could produce unexpected behaviour
            for (int i = 0; i < function.Expressions.Length; i++)
            {
                args.Parameters[i] = new Expression(function.Expressions[i], _options, _cultureInfo);
                args.Parameters[i].EvaluateFunction += EvaluateFunction;
                args.Parameters[i].EvaluateParameter += EvaluateParameter;

                // Assign the parameters of the Expression to the arguments so that custom Functions and Parameters can use them
                args.Parameters[i].Parameters = Parameters;
            }

            // Calls external implementation
            OnEvaluateFunction(IgnoreCase ? function.Identifier.Name.ToLower(_cultureInfo) : function.Identifier.Name, args);

            // If an external implementation was found get the result back
            if (args.HasResult)
            {
                Result = args.Result;
                return;
            }

            switch (function.Identifier.Name.ToUpperInvariant())
            {
                #region Abs
                case "ABS":

                    CheckCase("Abs", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Abs() takes exactly 1 argument");

                    bool useDouble = (_options & EvaluateOptions.UseDoubleForAbsFunction) == EvaluateOptions.UseDoubleForAbsFunction;
                    if (useDouble)
                    {
                        Result = Math.Abs(Convert.ToDouble(
                            Evaluate(function.Expressions[0]), _cultureInfo));
                    }
                    else
                    {
                        Result = Math.Abs(Convert.ToDecimal(
                            Evaluate(function.Expressions[0]), _cultureInfo));
                    }

                    break;

                #endregion

                #region Acos
                case "ACOS":

                    CheckCase("Acos", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Acos() takes exactly 1 argument");

                    Result = Math.Acos(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Asin
                case "ASIN":

                    CheckCase("Asin", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Asin() takes exactly 1 argument");

                    Result = Math.Asin(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Atan
                case "ATAN":

                    CheckCase("Atan", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Atan() takes exactly 1 argument");

                    Result = Math.Atan(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Ceiling
                case "CEILING":

                    CheckCase("Ceiling", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Ceiling() takes exactly 1 argument");

                    Result = Math.Ceiling(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Cos

                case "COS":

                    CheckCase("Cos", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Cos() takes exactly 1 argument");

                    Result = Math.Cos(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Exp
                case "EXP":

                    CheckCase("Exp", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Exp() takes exactly 1 argument");

                    Result = Math.Exp(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Floor
                case "FLOOR":

                    CheckCase("Floor", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Floor() takes exactly 1 argument");

                    Result = Math.Floor(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region IEEERemainder
                case "IEEEREMAINDER":

                    CheckCase("IEEERemainder", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("IEEERemainder() takes exactly 2 arguments");

                    Result = Math.IEEERemainder(Convert.ToDouble(Evaluate(function.Expressions[0]), _cultureInfo),
                        Convert.ToDouble(Evaluate(function.Expressions[1]), _cultureInfo));

                    break;

                #endregion

                #region if
                case "IF":

                    CheckCase("if", function.Identifier.Name);

                    if (function.Expressions.Length != 3)
                        throw new ArgumentException("if() takes exactly 3 arguments");

                    bool cond = Convert.ToBoolean(
                        Evaluate(function.Expressions[0]), _cultureInfo);

                    Result = cond ? Evaluate(function.Expressions[1]) : Evaluate(function.Expressions[2]);
                    break;

                #endregion

                #region in
                case "IN":

                    CheckCase("in", function.Identifier.Name);

                    if (function.Expressions.Length < 2)
                        throw new ArgumentException("in() takes at least 2 arguments");

                    object parameter = Evaluate(function.Expressions[0]);

                    bool evaluation = false;

                    // Goes through any values, and stop whe one is found
                    for (int i = 1; i < function.Expressions.Length; i++)
                    {
                        object argument = Evaluate(function.Expressions[i]);
                        if (CompareUsingMostPreciseType(parameter, argument) == 0)
                        {
                            evaluation = true;
                            break;
                        }
                    }

                    Result = evaluation;
                    break;

                #endregion

                #region Log
                case "LOG":

                    CheckCase("Log", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Log() takes exactly 2 arguments");

                    Result = Math.Log(Convert.ToDouble(Evaluate(function.Expressions[0]), _cultureInfo),
                        Convert.ToDouble(Evaluate(function.Expressions[1]), _cultureInfo));

                    break;

                #endregion

                #region Log10
                case "LOG10":

                    CheckCase("Log10", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Log10() takes exactly 1 argument");

                    Result = Math.Log10(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Max
                case "MAX":

                    CheckCase("Max", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Max() takes exactly 2 arguments");

                    object maxleft = Evaluate(function.Expressions[0]);
                    object maxright = Evaluate(function.Expressions[1]);

                    Result = Numbers.Max(maxleft, maxright, _cultureInfo);
                    break;

                #endregion

                #region Min
                case "MIN":

                    CheckCase("Min", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Min() takes exactly 2 arguments");

                    object minleft = Evaluate(function.Expressions[0]);
                    object minright = Evaluate(function.Expressions[1]);

                    Result = Numbers.Min(minleft, minright, _cultureInfo);
                    break;

                #endregion

                #region Pow
                case "POW":

                    CheckCase("Pow", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Pow() takes exactly 2 arguments");

                    Result = Math.Pow(Convert.ToDouble(Evaluate(function.Expressions[0]), _cultureInfo),
                        Convert.ToDouble(Evaluate(function.Expressions[1]), _cultureInfo));

                    break;

                #endregion

                #region Round
                case "ROUND":

                    CheckCase("Round", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Round() takes exactly 2 arguments");

                    MidpointRounding rounding = (_options & EvaluateOptions.RoundAwayFromZero) == EvaluateOptions.RoundAwayFromZero ? MidpointRounding.AwayFromZero : MidpointRounding.ToEven;

                    Result = Math.Round(Convert.ToDouble(Evaluate(function.Expressions[0]), _cultureInfo),
                        Convert.ToInt16(Evaluate(function.Expressions[1]), _cultureInfo), rounding);

                    break;

                #endregion

                #region Sign
                case "SIGN":

                    CheckCase("Sign", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Sign() takes exactly 1 argument");

                    Result = Math.Sign(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Sin
                case "SIN":

                    CheckCase("Sin", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Sin() takes exactly 1 argument");

                    Result = Math.Sin(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Sqrt
                case "SQRT":

                    CheckCase("Sqrt", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Sqrt() takes exactly 1 argument");

                    Result = Math.Sqrt(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Tan
                case "TAN":

                    CheckCase("Tan", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Tan() takes exactly 1 argument");

                    Result = Math.Tan(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                #region Truncate
                case "TRUNCATE":

                    CheckCase("Truncate", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Truncate() takes exactly 1 argument");

                    Result = Math.Truncate(Convert.ToDouble(
                        Evaluate(function.Expressions[0]), _cultureInfo));

                    break;

                #endregion

                default:
                    throw new ArgumentException("Function not found",
                        function.Identifier.Name);
            }
        }

        private void CheckCase(string function, string called)
        {
            if (!IgnoreCase && function != called)
                throw new ArgumentException($"Function not found {called}. Try {function} instead.");
        }

        public event EvaluateFunctionHandler EvaluateFunction;

        private void OnEvaluateFunction(string name, FunctionArgs args)
        {
            if (EvaluateFunction != null)
                EvaluateFunction(name, args);
        }

        public override void Visit(Identifier parameter)
        {
            // The parameter is defined in the hashtable
            if (Parameters.TryGetValue(parameter.Name, out object value))
            {
                // The parameter is itself another Expression
                if (value is Expression expression)
                {
                    // Overloads parameters
                    foreach (var p in Parameters)
                    {
                        expression.Parameters[p.Key] = p.Value;
                    }

                    expression.EvaluateFunction += EvaluateFunction;
                    expression.EvaluateParameter += EvaluateParameter;

                    Result = expression.Evaluate();
                }
                else
                    Result = value;
            }
            else
            {
                // The parameter should be defined in a call back method
                var args = new ParameterArgs();

                // Calls external implementation
                OnEvaluateParameter(parameter.Name, args);

                if (!args.HasResult)
                    throw new ArgumentException("Parameter was not defined", parameter.Name);

                Result = args.Result;
            }
        }

        public event EvaluateParameterHandler EvaluateParameter;

        private void OnEvaluateParameter(string name, ParameterArgs args)
        {
            if (EvaluateParameter != null)
                EvaluateParameter(name, args);
        }

        public Dictionary<string, object> Parameters { get; set; }
    }
}
