using System;
using System.Collections.Generic;

namespace NCalc.Domain
{
    public class EvaluationVisitor : LogicalExpressionVisitor
    {
        private delegate T Func<T>();

        private readonly EvaluateOptions _options = EvaluateOptions.None;

        private bool IgnoreCase { get { return (_options & EvaluateOptions.IgnoreCase) == EvaluateOptions.IgnoreCase; } }
        private bool Ordinal { get { return (_options & EvaluateOptions.MatchStringsOrdinal) == EvaluateOptions.MatchStringsOrdinal; } }
        private bool IgnoreCaseString { get { return (_options & EvaluateOptions.MatchStringsWithIgnoreCase) == EvaluateOptions.MatchStringsWithIgnoreCase; } }
        private bool Checked { get { return (_options & EvaluateOptions.OverflowProtection) == EvaluateOptions.OverflowProtection; } }

        public EvaluationVisitor(EvaluateOptions options)
        {
            _options = options;
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

            a = Convert.ChangeType(a, mpt);
            b = Convert.ChangeType(b, mpt);

            if (mpt.Equals(typeof(string)) && (Ordinal || IgnoreCaseString))
            {
                if (Ordinal)
                {
                    if (IgnoreCaseString) return StringComparer.OrdinalIgnoreCase.Compare(a?.ToString(), b?.ToString());
                    else StringComparer.Ordinal.Compare(a?.ToString(), b?.ToString());
                }
                else return StringComparer.CurrentCultureIgnoreCase.Compare(a?.ToString(), b?.ToString());
            }

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
            bool left = Convert.ToBoolean(Result);

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
                    Result = Convert.ToBoolean(left()) && Convert.ToBoolean(right());
                    break;

                case BinaryExpressionType.Or:
                    Result = Convert.ToBoolean(left()) || Convert.ToBoolean(right());
                    break;

                case BinaryExpressionType.Div:
                    //Actually doesn't need checked here, since if one is real,
                    // checked does nothing, and if they are int the result will only be same or smaller
                    // (since anything between 1 and 0 is not int and 0 is an exception anyway
                    Result = IsReal(left()) || IsReal(right())
                                 ? Numbers.Divide(left(), right(), _options)
                                 : Numbers.Divide(Convert.ToDouble(left()), right(), _options);
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
                        ? Numbers.SoustractChecked(left(), right(), _options)
                        : Numbers.Soustract(left(), right(), _options);
                    break;


                case BinaryExpressionType.Modulo:
                    Result = Numbers.Modulo(left(), right());
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
                            ? Numbers.AddChecked(left(), right(), _options)
                            : Numbers.Add(left(), right(), _options);
                    }

                    break;

                case BinaryExpressionType.Times:
                    Result = Checked
                        ? Numbers.MultiplyChecked(left(), right(), _options)
                        : Numbers.Multiply(left(), right(), _options);
                    break;

                case BinaryExpressionType.BitwiseAnd:
                    Result = Convert.ToUInt16(left()) & Convert.ToUInt16(right());
                    break;


                case BinaryExpressionType.BitwiseOr:
                    Result = Convert.ToUInt16(left()) | Convert.ToUInt16(right());
                    break;


                case BinaryExpressionType.BitwiseXOr:
                    Result = Convert.ToUInt16(left()) ^ Convert.ToUInt16(right());
                    break;


                case BinaryExpressionType.LeftShift:
                    Result = Convert.ToUInt16(left()) << Convert.ToUInt16(right());
                    break;


                case BinaryExpressionType.RightShift:
                    Result = Convert.ToUInt16(left()) >> Convert.ToUInt16(right());
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
                    Result = !Convert.ToBoolean(Result);
                    break;

                case UnaryExpressionType.Negate:
                    Result = Numbers.Soustract(0, Result, _options);
                    break;

                case UnaryExpressionType.BitwiseNot:
                    Result = ~Convert.ToUInt16(Result);
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
                args.Parameters[i] = new Expression(function.Expressions[i], _options);
                args.Parameters[i].EvaluateFunction += EvaluateFunction;
                args.Parameters[i].EvaluateParameter += EvaluateParameter;

                // Assign the parameters of the Expression to the arguments so that custom Functions and Parameters can use them
                args.Parameters[i].Parameters = Parameters;
            }

            // Calls external implementation
            OnEvaluateFunction(IgnoreCase ? function.Identifier.Name.ToLower() : function.Identifier.Name, args);

            // If an external implementation was found get the result back
            if (args.HasResult)
            {
                Result = args.Result;
                return;
            }

            switch (function.Identifier.Name)
            {
                #region Abs
                case string n when n.Equals("abs", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Abs", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Abs() takes exactly 1 argument");

                    bool useDouble = (_options & EvaluateOptions.UseDoubleForAbsFunction) == EvaluateOptions.UseDoubleForAbsFunction;
                    if (useDouble)
                    {
                        Result = Math.Abs(Convert.ToDouble(
                                                  Evaluate(function.Expressions[0]))
                        );
                    }
                    else
                    {
                        Result = Math.Abs(Convert.ToDecimal(
                                                  Evaluate(function.Expressions[0]))
                        );
                    }

                    break;

                #endregion

                #region Acos
                case string n when n.Equals("acos", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Acos", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Acos() takes exactly 1 argument");

                    Result = Math.Acos(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Asin
                case string n when n.Equals("asin", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Asin", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Asin() takes exactly 1 argument");

                    Result = Math.Asin(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Atan
                case string n when n.Equals("atan", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Atan", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Atan() takes exactly 1 argument");

                    Result = Math.Atan(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Ceiling
                case string n when n.Equals("ceiling", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Ceiling", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Ceiling() takes exactly 1 argument");

                    Result = Math.Ceiling(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Cos

                case string n when n.Equals("cos", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Cos", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Cos() takes exactly 1 argument");

                    Result = Math.Cos(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Exp
                case string n when n.Equals("exp", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Exp", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Exp() takes exactly 1 argument");

                    Result = Math.Exp(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Floor
                case string n when n.Equals("floor", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Floor", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Floor() takes exactly 1 argument");

                    Result = Math.Floor(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region IEEERemainder
                case string n when n.Equals("ieeeremainder", StringComparison.OrdinalIgnoreCase):

                    CheckCase("IEEERemainder", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("IEEERemainder() takes exactly 2 arguments");

                    Result = Math.IEEERemainder(Convert.ToDouble(Evaluate(function.Expressions[0])), Convert.ToDouble(Evaluate(function.Expressions[1])));

                    break;

                #endregion

                #region Log
                case string n when n.Equals("log", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Log", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Log() takes exactly 2 arguments");

                    Result = Math.Log(Convert.ToDouble(Evaluate(function.Expressions[0])), Convert.ToDouble(Evaluate(function.Expressions[1])));

                    break;

                #endregion

                #region Log10
                case string n when n.Equals("log10", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Log10", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Log10() takes exactly 1 argument");

                    Result = Math.Log10(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Pow
                case string n when n.Equals("pow", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Pow", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Pow() takes exactly 2 arguments");

                    Result = Math.Pow(Convert.ToDouble(Evaluate(function.Expressions[0])), Convert.ToDouble(Evaluate(function.Expressions[1])));

                    break;

                #endregion

                #region Round
                case string n when n.Equals("round", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Round", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Round() takes exactly 2 arguments");

                    MidpointRounding rounding = (_options & EvaluateOptions.RoundAwayFromZero) == EvaluateOptions.RoundAwayFromZero ? MidpointRounding.AwayFromZero : MidpointRounding.ToEven;

                    Result = Math.Round(Convert.ToDouble(Evaluate(function.Expressions[0])), Convert.ToInt16(Evaluate(function.Expressions[1])), rounding);

                    break;

                #endregion

                #region Sign
                case string n when n.Equals("sign", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Sign", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Sign() takes exactly 1 argument");

                    Result = Math.Sign(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Sin
                case string n when n.Equals("sin", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Sin", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Sin() takes exactly 1 argument");

                    Result = Math.Sin(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Sqrt
                case string n when n.Equals("sqrt", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Sqrt", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Sqrt() takes exactly 1 argument");

                    Result = Math.Sqrt(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Tan
                case string n when n.Equals("tan", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Tan", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Tan() takes exactly 1 argument");

                    Result = Math.Tan(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Truncate
                case string n when n.Equals("truncate", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Truncate", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Truncate() takes exactly 1 argument");

                    Result = Math.Truncate(Convert.ToDouble(Evaluate(function.Expressions[0])));

                    break;

                #endregion

                #region Max
                case string n when n.Equals("max", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Max", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Max() takes exactly 2 arguments");

                    object maxleft = Evaluate(function.Expressions[0]);
                    object maxright = Evaluate(function.Expressions[1]);

                    Result = Numbers.Max(maxleft, maxright);
                    break;

                #endregion

                #region Min
                case string n when n.Equals("min", StringComparison.OrdinalIgnoreCase):

                    CheckCase("Min", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Min() takes exactly 2 arguments");

                    object minleft = Evaluate(function.Expressions[0]);
                    object minright = Evaluate(function.Expressions[1]);

                    Result = Numbers.Min(minleft, minright);
                    break;

                #endregion

                #region if
                case string n when n.Equals("if", StringComparison.OrdinalIgnoreCase):

                    CheckCase("if", function.Identifier.Name);

                    if (function.Expressions.Length != 3)
                        throw new ArgumentException("if() takes exactly 3 arguments");

                    bool cond = Convert.ToBoolean(Evaluate(function.Expressions[0]));

                    Result = cond ? Evaluate(function.Expressions[1]) : Evaluate(function.Expressions[2]);
                    break;

                #endregion

                #region in
                case string n when n.Equals("in", StringComparison.OrdinalIgnoreCase):

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

                default:
                    throw new ArgumentException("Function not found",
                        function.Identifier.Name);
            }
        }

        private void CheckCase(string function, string called)
        {
            if (!IgnoreCase && function != called)
                throw new ArgumentException(String.Format("Function not found {0}. Try {1} instead.", called, function));
        }

        public event EvaluateFunctionHandler EvaluateFunction;

        private void OnEvaluateFunction(string name, FunctionArgs args)
        {
            if (EvaluateFunction != null)
                EvaluateFunction(name, args);
        }

        public override void Visit(Identifier parameter)
        {
            if (Parameters.ContainsKey(parameter.Name))
            {
                // The parameter is defined in the hashtable
                if (Parameters[parameter.Name] is Expression)
                {
                    // The parameter is itself another Expression
                    var expression = (Expression)Parameters[parameter.Name];

                    // Overloads parameters
                    foreach (var p in Parameters)
                    {
                        expression.Parameters[p.Key] = p.Value;
                    }

                    expression.EvaluateFunction += EvaluateFunction;
                    expression.EvaluateParameter += EvaluateParameter;

                    Result = ((Expression)Parameters[parameter.Name]).Evaluate();
                }
                else
                    Result = Parameters[parameter.Name];
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
