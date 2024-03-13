using FluentAssertions;
using NCalc.Domain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace NCalc.Tests
{
    public class Fixtures
    {
        private readonly ITestOutputHelper _output;

        public Fixtures(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ExpressionShouldEvaluate()
        {
            var expressions = new[]
            {
                "2 + 3 + 5",
                "2 * 3 + 5",
                "2 * (3 + 5)",
                "2 * (2*(2*(2+1)))",
                "10 % 3",
                "true or false",
                "not true",
                "false || not (false and true)",
                "3 > 2 and 1 <= (3-2)",
                "3 % 2 != 10 % 3"
            };

            foreach (string expression in expressions)
                _output.WriteLine("{0} = {1}",
                    expression,
                    Extensions.CreateExpression(expression).Evaluate());
        }

        [Fact]
        public void ExpressionShouldHandleNullRightParameters()
        {
            var e = Extensions.CreateExpression("'a string' == null", EvaluateOptions.AllowNullParameter);

            Assert.False((bool)e.Evaluate());
        }

        [Fact]
        public void ExpressionShouldHandleNullLeftParameters()
        {
            var e = Extensions.CreateExpression("null == 'a string'", EvaluateOptions.AllowNullParameter);

            Assert.False((bool)e.Evaluate());
        }

        [Fact]
        public void ExpressionShouldHandleNullBothParameters()
        {
            var e = Extensions.CreateExpression("null == null", EvaluateOptions.AllowNullParameter);

            Assert.True((bool)e.Evaluate());
        }

        [Fact]
        public void ShouldCompareNullToNull()
        {
            var e = Extensions.CreateExpression("[x] = null", EvaluateOptions.AllowNullParameter);

            e.Parameters["x"] = null;

            Assert.True((bool)e.Evaluate());
        }

        [Fact]
        public void ShouldCompareNullableToNonNullable()
        {
            var e = Extensions.CreateExpression("[x] = 5", EvaluateOptions.AllowNullParameter);

            e.Parameters["x"] = (int?)5;
            Assert.True((bool)e.Evaluate());

            e.Parameters["x"] = (int?)6;
            Assert.False((bool)e.Evaluate());
        }

        [Fact]
        public void ShouldCompareNullableNullToNonNullable()
        {
            var e = Extensions.CreateExpression("[x] = 5", EvaluateOptions.AllowNullParameter);

            e.Parameters["x"] = null;
            Assert.False((bool)e.Evaluate());
        }

        [Fact]
        public void ShouldCompareNullToString()
        {
            var e = Extensions.CreateExpression("[x] = 'foo'", EvaluateOptions.AllowNullParameter);

            e.Parameters["x"] = null;

            Assert.False((bool)e.Evaluate());
        }

        [Fact]
        public void ExpressionDoesNotDefineNullParameterWithoutNullOption()
        {
            var e = Extensions.CreateExpression("'a string' == null");

            var ex = Assert.Throws<ArgumentException>(() => e.Evaluate());
            Assert.Contains("Parameter was not defined", ex.Message);
        }

        [Fact]
        public void ExpressionThrowsNullReferenceExceptionWithoutNullOption()
        {
            var e = Extensions.CreateExpression("'a string' == null");

            e.Parameters["null"] = null;

            Assert.Throws<NullReferenceException>(() => e.Evaluate());
        }

        [Fact]
        public void ShouldEvaluateExcessiveNulls()
        {
            var e = Extensions.CreateExpression(GetNullsFormula(), EvaluateOptions.AllowNullParameter);

            Assert.Null(e.Evaluate());
        }

        [Fact]
        public void ShouldEvaluateExcessiveNullsInReasonableTime()
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            const int iterations = 1000;
            var formula = GetNullsFormula();

            for (int i = 0; i < iterations; i++)
            {
                Extensions.CreateExpression(formula, EvaluateOptions.AllowNullParameter).Evaluate();
            }

            stopwatch.Stop();

            const int targetMilliseconds = 100;
            Assert.True((stopwatch.ElapsedMilliseconds / iterations) <= targetMilliseconds, "Evaluation did not meet performance expectations");
        }

        private static string GetNullsFormula(int number = 100, string op = "+") => string.Join(op, Enumerable.Repeat("null", number));

        [Fact]
        public void ShouldParseValues()
        {
            Assert.Equal(123456, Extensions.CreateExpression("123456").Evaluate());
            Assert.Equal(new DateTime(2001, 01, 01), Extensions.CreateExpression("#01/01/2001#").Evaluate());
            Assert.Equal(0.2d, Extensions.CreateExpression(".2").Evaluate());
            Assert.Equal(123.456d, Extensions.CreateExpression("123.456").Evaluate());
            Assert.Equal(123d, Extensions.CreateExpression("123.").Evaluate());
            Assert.Equal(12300d, Extensions.CreateExpression("123.E2").Evaluate());
            Assert.Equal((object)true, Extensions.CreateExpression("true").Evaluate());
            Assert.Equal("true", Extensions.CreateExpression("'true'").Evaluate());
            Assert.Equal("azerty", Extensions.CreateExpression("'azerty'").Evaluate());
        }

        [Fact]
        public void ParsedExpressionToStringShouldHandleSmallDecimals()
        {
            // small decimals starting with 0 resulting in scientific notation did not work in original NCalc
            var equation = "0.000001";
            var testExpression = Extensions.CreateExpression(equation);
            testExpression.Evaluate();
            Assert.Equal(equation, testExpression.ParsedExpression.ToString());
        }

        [Fact]
        public void ShouldHandleUnicode()
        {
            Assert.Equal("経済協力開発機構", Extensions.CreateExpression("'経済協力開発機構'").Evaluate());
            Assert.Equal("Hello", Extensions.CreateExpression(@"'\u0048\u0065\u006C\u006C\u006F'").Evaluate());
            Assert.Equal("だ", Extensions.CreateExpression(@"'\u3060'").Evaluate());
            Assert.Equal("\u0100", Extensions.CreateExpression(@"'\u0100'").Evaluate());
        }

        [Fact]
        public void ShouldEscapeCharacters()
        {
            Assert.Equal("'hello'", Extensions.CreateExpression(@"'\'hello\''").Evaluate());
            Assert.Equal(" ' hel lo ' ", Extensions.CreateExpression(@"' \' hel lo \' '").Evaluate());
            Assert.Equal("hel\nlo", Extensions.CreateExpression(@"'hel\nlo'").Evaluate());
        }

        [Fact]
        public void ShouldDisplayErrorMessages()
        {
            try
            {
                Extensions.CreateExpression("(3 + 2").Evaluate();
                throw new Exception();
            }
            catch (EvaluationException e)
            {
                _output.WriteLine("Error catched: " + e.Message);
            }
        }

        [Fact]
        public void Maths()
        {
            Assert.Equal(1M, Extensions.CreateExpression("Abs(-1)").Evaluate());
            Assert.Equal(0d, Extensions.CreateExpression("Acos(1)").Evaluate());
            Assert.Equal(0d, Extensions.CreateExpression("Asin(0)").Evaluate());
            Assert.Equal(0d, Extensions.CreateExpression("Atan(0)").Evaluate());
            Assert.Equal(2d, Extensions.CreateExpression("Ceiling(1.5)").Evaluate());
            Assert.Equal(1d, Extensions.CreateExpression("Cos(0)").Evaluate());
            Assert.Equal(1d, Extensions.CreateExpression("Exp(0)").Evaluate());
            Assert.Equal(1d, Extensions.CreateExpression("Floor(1.5)").Evaluate());
            Assert.Equal(-1d, Extensions.CreateExpression("IEEERemainder(3,2)").Evaluate());
            Assert.Equal(0d, Extensions.CreateExpression("Log(1,10)").Evaluate());
            Assert.Equal(0d, Extensions.CreateExpression("Log10(1)").Evaluate());
            Assert.Equal(9d, Extensions.CreateExpression("Pow(3,2)").Evaluate());
            Assert.Equal(3.22d, Extensions.CreateExpression("Round(3.222,2)").Evaluate());
            Assert.Equal(-1, Extensions.CreateExpression("Sign(-10)").Evaluate());
            Assert.Equal(0d, Extensions.CreateExpression("Sin(0)").Evaluate());
            Assert.Equal(2d, Extensions.CreateExpression("Sqrt(4)").Evaluate());
            Assert.Equal(0d, Extensions.CreateExpression("Tan(0)").Evaluate());
            Assert.Equal(1d, Extensions.CreateExpression("Truncate(1.7)").Evaluate());
        }

        [Fact]
        public void ExpressionShouldEvaluateCustomFunctions()
        {
            var e = Extensions.CreateExpression("SecretOperation(3, 6)");

            e.EvaluateFunction += delegate (string name, FunctionArgs args)
                {
                    if (name == "SecretOperation")
                        args.Result = (int)args.Parameters[0].Evaluate() + (int)args.Parameters[1].Evaluate();
                };

            Assert.Equal(9, e.Evaluate());
        }

        [Fact]
        public void ExpressionShouldEvaluateCustomFunctionsWithParameters()
        {
            var e = Extensions.CreateExpression("SecretOperation([e], 6) + f");
            e.Parameters["e"] = 3;
            e.Parameters["f"] = 1;

            e.EvaluateFunction += delegate (string name, FunctionArgs args)
                {
                    if (name == "SecretOperation")
                        args.Result = (int)args.Parameters[0].Evaluate() + (int)args.Parameters[1].Evaluate();
                };

            Assert.Equal(10, e.Evaluate());
        }

        [Fact]
        public void ExpressionShouldEvaluateParameters()
        {
            var e = Extensions.CreateExpression("Round(Pow(Pi, 2) + Pow([Pi Squared], 2) + [X], 2)");

            e.Parameters["Pi Squared"] = Extensions.CreateExpression("Pi * [Pi]");
            e.Parameters["X"] = 10;

            e.EvaluateParameter += delegate (string name, ParameterArgs args)
                {
                    if (name == "Pi")
                        args.Result = 3.14;
                };

            Assert.Equal(117.07, e.Evaluate());
        }

        [Fact]
        public void ShouldEvaluateConditionnal()
        {
            var eif = Extensions.CreateExpression("if([divider] <> 0, [divided] / [divider], 0)");
            eif.Parameters["divider"] = 5;
            eif.Parameters["divided"] = 5;

            Assert.Equal(1d, eif.Evaluate());

            eif = Extensions.CreateExpression("if([divider] <> 0, [divided] / [divider], 0)");
            eif.Parameters["divider"] = 0;
            eif.Parameters["divided"] = 5;
            Assert.Equal(0, eif.Evaluate());
        }

        [Fact]
        public void ShouldOverrideExistingFunctions()
        {
            var e = Extensions.CreateExpression("Round(1.99, 2)");

            Assert.Equal(1.99d, e.Evaluate());

            e.EvaluateFunction += delegate (string name, FunctionArgs args)
            {
                if (name == "Round")
                    args.Result = 3;
            };

            Assert.Equal(3, e.Evaluate());
        }

        [Fact]
        public void ShouldEvaluateInOperator()
        {
            // The last argument should not be evaluated
            var ein = Extensions.CreateExpression("in((2 + 2), [1], [2], 1 + 2, 4, 1 / 0)");
            ein.Parameters["1"] = 2;
            ein.Parameters["2"] = 5;

            Assert.Equal((object)true, ein.Evaluate());

            var eout = Extensions.CreateExpression("in((2 + 2), [1], [2], 1 + 2, 3)");
            eout.Parameters["1"] = 2;
            eout.Parameters["2"] = 5;

            Assert.Equal((object)false, eout.Evaluate());

            // Should work with strings
            var estring = Extensions.CreateExpression("in('to' + 'to', 'titi', 'toto')");

            Assert.Equal((object)true, estring.Evaluate());

        }

        [Fact]
        public void ShouldEvaluateOperators()
        {
            var expressions = new Dictionary<string, object>
                                  {
                                      {"!true", false},
                                      {"not false", true},
                                      {"Not false", true},
                                      {"NOT false", true},
                                      {"-10", -10},
                                      {"+20", 20},
                                      {"2**-1", 0.5},
                                      {"2**+2", 4.0},
                                      {"2 * 3", 6},
                                      {"6 / 2", 3d},
                                      {"7 % 2", 1},
                                      {"2 + 3", 5},
                                      {"2 - 1", 1},
                                      {"1 < 2", true},
                                      {"1 > 2", false},
                                      {"1 <= 2", true},
                                      {"1 <= 1", true},
                                      {"1 >= 2", false},
                                      {"1 >= 1", true},
                                      {"1 = 1", true},
                                      {"1 == 1", true},
                                      {"1 != 1", false},
                                      {"1 <> 1", false},
                                      {"1 & 1", 1},
                                      {"1 | 1", 1},
                                      {"1 ^ 1", 0},
                                      {"~1", ~1},
                                      {"2 >> 1", 1},
                                      {"2 << 1", 4},
                                      {"true && false", false},
                                      {"True and False", false},
                                      {"tRue aNd faLse", false},
                                      {"TRUE ANd fALSE", false},
                                      {"true AND FALSE", false},
                                      {"true and false", false},
                                      {"true || false", true},
                                      {"true or false", true},
                                      {"true Or false", true},
                                      {"true OR false", true},
                                      {"if(true, 0, 1)", 0},
                                      {"if(false, 0, 1)", 1}
                                  };

            foreach (KeyValuePair<string, object> pair in expressions)
            {
                Assert.Equal(pair.Value, Extensions.CreateExpression(pair.Key).Evaluate());
            }

        }

        [Fact]
        public void ShouldHandleOperatorsPriority()
        {
            Assert.Equal(8, Extensions.CreateExpression("2+2+2+2").Evaluate());
            Assert.Equal(16, Extensions.CreateExpression("2*2*2*2").Evaluate());
            Assert.Equal(6, Extensions.CreateExpression("2*2+2").Evaluate());
            Assert.Equal(6, Extensions.CreateExpression("2+2*2").Evaluate());

            Assert.Equal(9d, Extensions.CreateExpression("1 + 2 + 3 * 4 / 2").Evaluate());
            Assert.Equal(13.5, Extensions.CreateExpression("18/2/2*3").Evaluate());
            Assert.Equal(-1d, Extensions.CreateExpression("-1 ** 2").Evaluate());
            Assert.Equal(1d, Extensions.CreateExpression("(-1) ** 2").Evaluate());
            Assert.Equal(512d, Extensions.CreateExpression("2 ** 3 ** 2").Evaluate());
            Assert.Equal(64d, Extensions.CreateExpression("(2 ** 3) ** 2").Evaluate());
            Assert.Equal(18d, Extensions.CreateExpression("2 * 3 ** 2").Evaluate());
            Assert.Equal(8d, Extensions.CreateExpression("2 ** 4 / 2").Evaluate());
        }

        [Fact]
        public void ShouldNotLoosePrecision()
        {
            Assert.Equal(0.5, Extensions.CreateExpression("3/6").Evaluate());
        }

        [Fact]
        public void ShouldThrowAnExpcetionWhenInvalidNumber()
        {
            try
            {
                Extensions.CreateExpression(". + 2").Evaluate();
                throw new Exception();
            }
            catch (EvaluationException e)
            {
                _output.WriteLine("Error catched: " + e.Message);
            }
        }

        [Fact]
        public void ShouldNotRoundDecimalValues()
        {
            Assert.Equal((object)false, Extensions.CreateExpression("0 <= -0.6").Evaluate());
        }

        [Fact]
        public void ShouldEvaluateTernaryExpression()
        {
            Assert.Equal(1, Extensions.CreateExpression("1+2<3 ? 3+4 : 1").Evaluate());
        }

        [Fact]
        public void ShouldSerializeExpression()
        {
            Assert.Equal("True and False", new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false)).ToString());
            Assert.Equal("1 / 2", new BinaryExpression(BinaryExpressionType.Div, new ValueExpression(1), new ValueExpression(2)).ToString());
            Assert.Equal("1 = 2", new BinaryExpression(BinaryExpressionType.Equal, new ValueExpression(1), new ValueExpression(2)).ToString());
            Assert.Equal("1 > 2", new BinaryExpression(BinaryExpressionType.Greater, new ValueExpression(1), new ValueExpression(2)).ToString());
            Assert.Equal("1 >= 2", new BinaryExpression(BinaryExpressionType.GreaterOrEqual, new ValueExpression(1), new ValueExpression(2)).ToString());
            Assert.Equal("1 < 2", new BinaryExpression(BinaryExpressionType.Lesser, new ValueExpression(1), new ValueExpression(2)).ToString());
            Assert.Equal("1 <= 2", new BinaryExpression(BinaryExpressionType.LesserOrEqual, new ValueExpression(1), new ValueExpression(2)).ToString());
            Assert.Equal("1 - 2", new BinaryExpression(BinaryExpressionType.Minus, new ValueExpression(1), new ValueExpression(2)).ToString());
            Assert.Equal("1 % 2", new BinaryExpression(BinaryExpressionType.Modulo, new ValueExpression(1), new ValueExpression(2)).ToString());
            Assert.Equal("1 != 2", new BinaryExpression(BinaryExpressionType.NotEqual, new ValueExpression(1), new ValueExpression(2)).ToString());
            Assert.Equal("True or False", new BinaryExpression(BinaryExpressionType.Or, new ValueExpression(true), new ValueExpression(false)).ToString());
            Assert.Equal("1 + 2", new BinaryExpression(BinaryExpressionType.Plus, new ValueExpression(1), new ValueExpression(2)).ToString());
            Assert.Equal("1 * 2", new BinaryExpression(BinaryExpressionType.Times, new ValueExpression(1), new ValueExpression(2)).ToString());

            Assert.Equal("-(True and False)", new UnaryExpression(UnaryExpressionType.Negate, new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false))).ToString());
            Assert.Equal("!(True and False)", new UnaryExpression(UnaryExpressionType.Not, new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false))).ToString());

            Assert.Equal("test(True and False, -(True and False))", new Function(new Identifier("test"), new LogicalExpression[] { new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false)), new UnaryExpression(UnaryExpressionType.Negate, new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false))) }).ToString());

            Assert.Equal("True", new ValueExpression(true).ToString());
            Assert.Equal("False", new ValueExpression(false).ToString());
            Assert.Equal("1", new ValueExpression(1).ToString());
            Assert.Equal("1.234", new ValueExpression(1.234).ToString());
            Assert.Equal("'hello'", new ValueExpression("hello").ToString());
            Assert.Equal("#" + new DateTime(2009, 1, 1) + "#", new ValueExpression(new DateTime(2009, 1, 1)).ToString());

            Assert.Equal("Sum(1 + 2)", new Function(new Identifier("Sum"), new[] { new BinaryExpression(BinaryExpressionType.Plus, new ValueExpression(1), new ValueExpression(2)) }).ToString());
        }

        [Fact]
        public void ShouldHandleStringConcatenation()
        {
            Assert.Equal("toto", Extensions.CreateExpression("'to' + 'to'").Evaluate());
            Assert.Equal("one2", Extensions.CreateExpression("'one' + 2").Evaluate());
            Assert.Equal(3M, Extensions.CreateExpression("1 + '2'").Evaluate());
        }

        [Fact]
        public void ShouldDetectSyntaxErrorsBeforeEvaluation()
        {
            var e = Extensions.CreateExpression("a + b * (");
            Assert.Null(e.Error);
            Assert.True(e.HasErrors());
            Assert.True(e.HasErrors());
            Assert.NotNull(e.Error);

            e = Extensions.CreateExpression("* b ");
            Assert.Null(e.Error);
            Assert.True(e.HasErrors());
            Assert.NotNull(e.Error);
        }

        [Fact]
        public void ShouldReuseCompiledExpressionsInMultiThreadedMode()
        {
            // Repeats the tests n times
            for (int cpt = 0; cpt < 20; cpt++)
            {
                const int nbthreads = 30;
                _exceptions = new List<Exception>();
                var threads = new Thread[nbthreads];

                // Starts threads
                for (int i = 0; i < nbthreads; i++)
                {
                    var thread = new Thread(WorkerThread);
                    thread.Start();
                    threads[i] = thread;
                }

                // Waits for end of threads
                bool running = true;
                while (running)
                {
                    Thread.Sleep(100);
                    running = false;
                    for (int i = 0; i < nbthreads; i++)
                    {
                        if (threads[i].ThreadState == ThreadState.Running)
                            running = true;
                    }
                }

                if (_exceptions.Count > 0)
                {
                    _output.WriteLine(_exceptions[0].StackTrace);
                    throw _exceptions[0];
                }
            }
        }

        private List<Exception> _exceptions = new();

        private void WorkerThread()
        {
            try
            {
                var r1 = new Random((int)DateTime.Now.Ticks);
                var r2 = new Random((int)DateTime.Now.Ticks);
                int n1 = r1.Next(10);
                int n2 = r2.Next(10);

                // Constructs a simple addition randomly. Odds are that the same expression gets constructed multiple times by different threads
                var exp = n1 + " + " + n2;
                var e = Extensions.CreateExpression(exp);
                Assert.True(e.Evaluate().Equals(n1 + n2));
            }
            catch (Exception e)
            {
                _exceptions.Add(e);
            }
        }

        [Fact]
        public void ShouldHandleCaseSensitiveness()
        {
            Assert.Equal(1M, Extensions.CreateExpression("aBs(-1)", EvaluateOptions.IgnoreCase).Evaluate());
            Assert.Equal(1M, Extensions.CreateExpression("Abs(-1)", EvaluateOptions.None).Evaluate());

            try
            {
                Assert.Equal(1M, Extensions.CreateExpression("aBs(-1)", EvaluateOptions.None).Evaluate());
            }
            catch (ArgumentException)
            {
                return;
            }
            catch (Exception)
            {
                throw new Exception("Unexpected exception");
            }

            throw new Exception("Should throw ArgumentException");
        }

        [Fact]
        public void ShouldHandleCustomParametersWhenNoSpecificParameterIsDefined()
        {
            var e = Extensions.CreateExpression("Round(Pow([Pi], 2) + Pow([Pi], 2) + 10, 2)");

            e.EvaluateParameter += delegate (string name, ParameterArgs arg)
            {
                if (name == "Pi")
                    arg.Result = 3.14;
            };

            e.Evaluate();
        }

        [Fact]
        public void ShouldHandleCustomFunctionsInFunctions()
        {
            var e = Extensions.CreateExpression("if(true, func1(x) + func2(func3(y)), 0)");

            e.EvaluateFunction += delegate (string name, FunctionArgs arg)
            {
                switch (name)
                {
                    case "func1":
                        arg.Result = 1;
                        break;
                    case "func2":
                        arg.Result = 2 * Convert.ToDouble(arg.Parameters[0].Evaluate());
                        break;
                    case "func3":
                        arg.Result = 3 * Convert.ToDouble(arg.Parameters[0].Evaluate());
                        break;
                }
            };

            e.EvaluateParameter += delegate (string name, ParameterArgs arg)
            {
                switch (name)
                {
                    case "x":
                        arg.Result = 1;
                        break;
                    case "y":
                        arg.Result = 2;
                        break;
                    case "z":
                        arg.Result = 3;
                        break;
                }
            };

            Assert.Equal(13d, e.Evaluate());
        }


        [Fact]
        public void ShouldParseScientificNotation()
        {
            Assert.Equal(12.2d, Extensions.CreateExpression("1.22e1").Evaluate());
            Assert.Equal(100d, Extensions.CreateExpression("1e2").Evaluate());
            Assert.Equal(100d, Extensions.CreateExpression("1e+2").Evaluate());
            Assert.Equal(0.01d, Extensions.CreateExpression("1e-2").Evaluate());
            Assert.Equal(0.001d, Extensions.CreateExpression(".1e-2").Evaluate());
            Assert.Equal(10000000000d, Extensions.CreateExpression("1e10").Evaluate());
        }

        [Fact]
        public void ShouldEvaluateArrayParameters()
        {
            var e = Extensions.CreateExpression("x * x", EvaluateOptions.IterateParameters);
            e.Parameters["x"] = new[] { 0, 1, 2, 3, 4 };

            var result = (IList)e.Evaluate();

            Assert.Equal(0, result[0]);
            Assert.Equal(1, result[1]);
            Assert.Equal(4, result[2]);
            Assert.Equal(9, result[3]);
            Assert.Equal(16, result[4]);
        }

        [Fact]
        public void CustomFunctionShouldReturnNull()
        {
            var e = Extensions.CreateExpression("SecretOperation(3, 6)");

            e.EvaluateFunction += delegate (string name, FunctionArgs args)
            {
                Assert.False(args.HasResult);
                if (name == "SecretOperation")
                    args.Result = null;
                Assert.True(args.HasResult);
            };

            Assert.Null(e.Evaluate());
        }

        [Fact]
        public void CustomParametersShouldReturnNull()
        {
            var e = Extensions.CreateExpression("x");

            e.EvaluateParameter += delegate (string name, ParameterArgs args)
            {
                Assert.False(args.HasResult);
                if (name == "x")
                    args.Result = null;
                Assert.True(args.HasResult);
            };

            Assert.Null(e.Evaluate());
        }

        [Fact]
        public void ShouldCompareDates()
        {
            Assert.Equal((object)true, Extensions.CreateExpression("#1/1/2009#==#1/1/2009#").Evaluate());
            Assert.Equal((object)false, Extensions.CreateExpression("#2/1/2009#==#1/1/2009#").Evaluate());
        }

        [Fact]
        public void ShouldRoundAwayFromZero()
        {
            Assert.Equal(22d, Extensions.CreateExpression("Round(22.5, 0)").Evaluate());
            Assert.Equal(23d, Extensions.CreateExpression("Round(22.5, 0)", EvaluateOptions.RoundAwayFromZero).Evaluate());
        }

        [Fact]
        public void ShouldEvaluateSubExpressions()
        {
            var volume = Extensions.CreateExpression("[surface] * h");
            var surface = Extensions.CreateExpression("[l] * [L]");
            volume.Parameters["surface"] = surface;
            volume.Parameters["h"] = 3;
            surface.Parameters["l"] = 1;
            surface.Parameters["L"] = 2;

            Assert.Equal(6, volume.Evaluate());
        }

        [Fact]
        public void ShouldHandleLongValues()
        {
            var expression = Extensions.CreateExpression("40000000000+1");
            Assert.Equal(40000000000 + 1L, expression.Evaluate());
        }

        [Fact]
        public void ShouldCompareLongValues()
        {
            var expression = Extensions.CreateExpression("(0=1500000)||(((0+2200000000)-1500000)<0)");
            Assert.Equal((object)false, expression.Evaluate());
        }

        [Fact]
        public void ShouldDisplayErrorIfUncompatibleTypes()
        {
            var e = Extensions.CreateExpression("(a > b) + 10");
            e.Parameters["a"] = 1;
            e.Parameters["b"] = 2;
            Assert.Throws<InvalidOperationException>(() => e.Evaluate());
        }

        [Theory]
        [InlineData("(X1 = 1)/2", 0.5)]
        [InlineData("(X1 = 1)*2", 2)]
        [InlineData("(X1 = 1)+1", 2)]
        [InlineData("(X1 = 1)-1", 0)]
        [InlineData("2*(X1 = 1)", 2)]
        [InlineData("2/(X1 = 1)", 2.0)]
        [InlineData("1+(X1 = 1)", 2)]
        [InlineData("1-(X1 = 1)", 0)]
        public void ShouldOptionallyCalculateWithBoolean(string formula, object expectedValue)
        {
            var expression = Extensions.CreateExpression(formula, EvaluateOptions.BooleanCalculation);
            expression.Parameters["X1"] = 1;

            expression.Evaluate().Should().Be(expectedValue);

            var lambda = expression.ToLambda<object>();
            lambda().Should().Be(expectedValue);
        }

        [Fact]
        public void ShouldNotConvertRealTypes()
        {
            var e = Extensions.CreateExpression("x/2");
            e.Parameters["x"] = 2F;
            Assert.Equal(typeof(float), e.Evaluate().GetType());

            e = Extensions.CreateExpression("x/2");
            e.Parameters["x"] = 2D;
            Assert.Equal(typeof(double), e.Evaluate().GetType());

            e = Extensions.CreateExpression("x/2");
            e.Parameters["x"] = 2m;
            Assert.Equal(typeof(decimal), e.Evaluate().GetType());

            e = Extensions.CreateExpression("a / b * 100");
            e.Parameters["a"] = 20M;
            e.Parameters["b"] = 20M;
            Assert.Equal(100M, e.Evaluate());

        }

        [Fact]
        public void ShouldShortCircuitBooleanExpressions()
        {
            var e = Extensions.CreateExpression("([a] != 0) && ([b]/[a]>2)");
            e.Parameters["a"] = 0;

            Assert.Equal((object)false, e.Evaluate());
        }

        [Fact]
        public void ShouldAddDoubleAndDecimal()
        {
            var e = Extensions.CreateExpression("1.8 + Abs([var1])");
            e.Parameters["var1"] = 9.2;

            Assert.Equal(11M, e.Evaluate());
        }

        [Fact]
        public void ShouldSubtractDoubleAndDecimal()
        {
            var e = Extensions.CreateExpression("[double] - [decimal]");
            e.Parameters["double"] = 2D;
            e.Parameters["decimal"] = 2m;

            Assert.Equal(0m, e.Evaluate());
        }

        [Fact]
        public void ShouldMultiplyDoubleAndDecimal()
        {
            var e = Extensions.CreateExpression("[double] * [decimal]");
            e.Parameters["double"] = 2D;
            e.Parameters["decimal"] = 2m;

            Assert.Equal(4m, e.Evaluate());
        }

        [Fact]
        public void ShouldDivideDoubleAndDecimal()
        {
            var e = Extensions.CreateExpression("[double] / [decimal]");
            e.Parameters["double"] = 2D;
            e.Parameters["decimal"] = 2m;

            Assert.Equal(1m, e.Evaluate());
        }

        [Fact]
        public void ShouldModDoubleAndDecimal()
        {
            var e = Extensions.CreateExpression("[double] % [decimal]");
            e.Parameters["double"] = 2D;
            e.Parameters["decimal"] = 2m;

            Assert.Equal(0m, e.Evaluate());
        }

        [InlineData("Min(2,1.97)", 1.97)]
        [InlineData("Max(2,2.33)", 2.33)]
        [Theory]
        public void ShouldCheckPrecisionOfBothParametersForMaxAndMin(string expression, double expected)
        {
            var e = Extensions.CreateExpression(expression);

            var result = e.Evaluate();

            Assert.Equal(expected, result);
        }

        // https://github.com/sklose/NCalc2/issues/54
        [Fact]
        [Trait("Category", "Integration")]
        public void Issue54()
        {
            const long expected = 9999999999L;
            var expression = $"if(true, {expected}, 0)";
            var e = Extensions.CreateExpression(expression);

            var actual = e.Evaluate();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldParseInvariantCulture()
        {
            var originalCulture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            try
            {
                var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                culture.NumberFormat.NumberDecimalSeparator = ",";
                Thread.CurrentThread.CurrentCulture = culture;

                Assert.Throws<FormatException>(() =>
                {
                    var expr = new Expression("[a] < 2.0") { Parameters = { ["a"] = "1.7" } };
                    expr.Evaluate();
                });

                var e = new Expression("[a]<2.0", CultureInfo.InvariantCulture) { Parameters = { ["a"] = "1.7" } };
                Assert.Equal(true, e.Evaluate());
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Fact]
        public void ShouldCorrectlyParseCustomCultureParameter()
        {
            var cultureDot = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            cultureDot.NumberFormat.NumberGroupSeparator = " ";
            var cultureComma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            cultureComma.NumberFormat.CurrencyDecimalSeparator = ",";
            cultureComma.NumberFormat.NumberGroupSeparator = " ";

            //use 1*[A] to avoid evaluating expression parameters as string - force numeric conversion
            ExecuteTest("1*[A]-[B]", 1.5m);
            ExecuteTest("1*[A]+[B]", 2.5m);
            ExecuteTest("1*[A]/[B]", 4m);
            ExecuteTest("1*[A]*[B]", 1m);
            ExecuteTest("1*[A]>[B]", true);
            ExecuteTest("1*[A]<[B]", false);

            void ExecuteTest(string formula, object expectedValue)
            {
                //Correctly evaluate with decimal dot culture and parameter with dot
                var expression = Extensions.CreateExpression(formula, cultureDot);
                expression.Parameters["A"] = "2.0";
                expression.Parameters["B"] = "0.5";
                Assert.Equal(expectedValue, expression.Evaluate());

                //Correctly evaluate with decimal comma and parameter with comma
                expression = Extensions.CreateExpression(formula, cultureComma);
                expression.Parameters["A"] = "2.0";
                expression.Parameters["B"] = "0.5";
                Assert.Equal(expectedValue, expression.Evaluate());

                //combining decimal dot and comma fails
                expression = Extensions.CreateExpression(formula, cultureComma);
                expression.Parameters["A"] = "2,0";
                expression.Parameters["B"] = "0.5";
                Assert.Throws<FormatException>(() => expression.Evaluate());

                //combining decimal dot and comma fails
                expression = Extensions.CreateExpression(formula, cultureDot);
                expression.Parameters["A"] = "2,0";
                expression.Parameters["B"] = "0.5";
                Assert.Throws<FormatException>(() => expression.Evaluate());
            }
        }
    }
}

