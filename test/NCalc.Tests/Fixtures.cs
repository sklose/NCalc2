using System;
using NCalc.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Collections;
using FluentAssertions;
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
            var expressions = new []
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
                    new Expression(expression).Evaluate());
        }

        [Fact]
        public void ShouldParseValues()
        {
            Assert.Equal(123456, new Expression("123456").Evaluate());
            Assert.Equal(new DateTime(2001, 01, 01), new Expression("#01/01/2001#").Evaluate());
            Assert.Equal(123.456d, new Expression("123.456").Evaluate());
            Assert.Equal(true, new Expression("true").Evaluate());
            Assert.Equal("true", new Expression("'true'").Evaluate());
            Assert.Equal("azerty", new Expression("'azerty'").Evaluate());
        }

        [Fact]
        public void ParsedExpressionToStringShouldHandleSmallDecimals()
        {
            // small decimals starting with 0 resulting in scientific notation did not work in original NCalc
            var equation = "0.000001";
            var testExpression = new Expression(equation);
            testExpression.Evaluate();
            Assert.Equal(equation, testExpression.ParsedExpression.ToString());
        }

        [Fact]
        public void ShouldHandleUnicode()
        {
            Assert.Equal("経済協力開発機構", new Expression("'経済協力開発機構'").Evaluate());
            Assert.Equal("Hello", new Expression(@"'\u0048\u0065\u006C\u006C\u006F'").Evaluate());
            Assert.Equal("だ", new Expression(@"'\u3060'").Evaluate());
            Assert.Equal("\u0100", new Expression(@"'\u0100'").Evaluate());
        }

        [Fact]
        public void ShouldEscapeCharacters()
        {
            Assert.Equal("'hello'", new Expression(@"'\'hello\''").Evaluate());
            Assert.Equal(" ' hel lo ' ", new Expression(@"' \' hel lo \' '").Evaluate());
            Assert.Equal("hel\nlo", new Expression(@"'hel\nlo'").Evaluate());
        }

        [Fact]
        public void ShouldDisplayErrorMessages()
        {
            try
            {
                new Expression("(3 + 2").Evaluate();
                throw new Exception();
            }
            catch(EvaluationException e)
            {
                _output.WriteLine("Error catched: " + e.Message);
            }
        }

        [Fact]
        public void Maths()
        {
            Assert.Equal(1M, new Expression("Abs(-1)").Evaluate());
            Assert.Equal(0d, new Expression("Acos(1)").Evaluate());
            Assert.Equal(0d, new Expression("Asin(0)").Evaluate());
            Assert.Equal(0d, new Expression("Atan(0)").Evaluate());
            Assert.Equal(2d, new Expression("Ceiling(1.5)").Evaluate());
            Assert.Equal(1d, new Expression("Cos(0)").Evaluate());
            Assert.Equal(1d, new Expression("Exp(0)").Evaluate());
            Assert.Equal(1d, new Expression("Floor(1.5)").Evaluate());
            Assert.Equal(-1d, new Expression("IEEERemainder(3,2)").Evaluate());
            Assert.Equal(0d, new Expression("Log(1,10)").Evaluate());
            Assert.Equal(0d, new Expression("Log10(1)").Evaluate());
            Assert.Equal(9d, new Expression("Pow(3,2)").Evaluate());
            Assert.Equal(3.22d, new Expression("Round(3.222,2)").Evaluate());
            Assert.Equal(-1, new Expression("Sign(-10)").Evaluate());
            Assert.Equal(0d, new Expression("Sin(0)").Evaluate());
            Assert.Equal(2d, new Expression("Sqrt(4)").Evaluate());
            Assert.Equal(0d, new Expression("Tan(0)").Evaluate());
            Assert.Equal(1d, new Expression("Truncate(1.7)").Evaluate());
        }

        [Fact]
        public void ExpressionShouldEvaluateCustomFunctions()
        {
            var e = new Expression("SecretOperation(3, 6)");

            e.EvaluateFunction += delegate(string name, FunctionArgs args)
                {
                    if (name == "SecretOperation")
                        args.Result = (int)args.Parameters[0].Evaluate() + (int)args.Parameters[1].Evaluate();
                };

            Assert.Equal(9, e.Evaluate());
        }

        [Fact]
        public void ExpressionShouldEvaluateCustomFunctionsWithParameters()
        {
            var e = new Expression("SecretOperation([e], 6) + f");
            e.Parameters["e"] = 3;
            e.Parameters["f"] = 1;

            e.EvaluateFunction += delegate(string name, FunctionArgs args)
                {
                    if (name == "SecretOperation")
                        args.Result = (int)args.Parameters[0].Evaluate() + (int)args.Parameters[1].Evaluate();
                };

            Assert.Equal(10, e.Evaluate());
        }

        [Fact]
        public void ExpressionShouldEvaluateParameters()
        {
            var e = new Expression("Round(Pow(Pi, 2) + Pow([Pi Squared], 2) + [X], 2)");

            e.Parameters["Pi Squared"] = new Expression("Pi * [Pi]");
            e.Parameters["X"] = 10;

            e.EvaluateParameter += delegate(string name, ParameterArgs args)
                {
                    if (name == "Pi")
                        args.Result = 3.14;
                };

            Assert.Equal(117.07, e.Evaluate());
        }

        [Fact]
        public void ShouldEvaluateConditionnal()
        {
            var eif = new Expression("if([divider] <> 0, [divided] / [divider], 0)");
            eif.Parameters["divider"] = 5;
            eif.Parameters["divided"] = 5;

            Assert.Equal(1d, eif.Evaluate());

            eif = new Expression("if([divider] <> 0, [divided] / [divider], 0)");
            eif.Parameters["divider"] = 0;
            eif.Parameters["divided"] = 5;
            Assert.Equal(0, eif.Evaluate());
        }

        [Fact]
        public void ShouldOverrideExistingFunctions()
        {
            var e = new Expression("Round(1.99, 2)");

            Assert.Equal(1.99d, e.Evaluate());

            e.EvaluateFunction += delegate(string name, FunctionArgs args)
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
            var ein = new Expression("in((2 + 2), [1], [2], 1 + 2, 4, 1 / 0)");
            ein.Parameters["1"] = 2;
            ein.Parameters["2"] = 5;

            Assert.Equal(true, ein.Evaluate());

            var eout = new Expression("in((2 + 2), [1], [2], 1 + 2, 3)");
            eout.Parameters["1"] = 2;
            eout.Parameters["2"] = 5;

            Assert.Equal(false, eout.Evaluate());

            // Should work with strings
            var estring = new Expression("in('to' + 'to', 'titi', 'toto')");

            Assert.Equal(true, estring.Evaluate());

        }

        [Fact]
        public void ShouldEvaluateOperators()
        {
            var expressions = new Dictionary<string, object>
                                  {
                                      {"!true", false},
                                      {"not false", true},
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
                                      {"true and false", false},
                                      {"true || false", true},
                                      {"true or false", true},
                                      {"if(true, 0, 1)", 0},
                                      {"if(false, 0, 1)", 1}
                                  };

            foreach (KeyValuePair<string, object> pair in expressions)
            {
                Assert.Equal(pair.Value, new Expression(pair.Key).Evaluate());
            }

        }

        [Fact]
        public void ShouldHandleOperatorsPriority()
        {
            Assert.Equal(8, new Expression("2+2+2+2").Evaluate());
            Assert.Equal(16, new Expression("2*2*2*2").Evaluate());
            Assert.Equal(6, new Expression("2*2+2").Evaluate());
            Assert.Equal(6, new Expression("2+2*2").Evaluate());

            Assert.Equal(9d, new Expression("1 + 2 + 3 * 4 / 2").Evaluate());
            Assert.Equal(13.5, new Expression("18/2/2*3").Evaluate());
        }

        [Fact]
        public void ShouldNotLoosePrecision()
        {
            Assert.Equal(0.5, new Expression("3/6").Evaluate());
        }

        [Fact]
        public void ShouldThrowAnExpcetionWhenInvalidNumber()
        {
            try
            {
                new Expression("4. + 2").Evaluate();
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
            Assert.Equal(false, new Expression("0 <= -0.6").Evaluate());
        }

        [Fact]
        public void ShouldEvaluateTernaryExpression()
        {
            Assert.Equal(1, new Expression("1+2<3 ? 3+4 : 1").Evaluate());
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

            Assert.Equal("-(True and False)",new UnaryExpression(UnaryExpressionType.Negate, new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false))).ToString());
            Assert.Equal("!(True and False)",new UnaryExpression(UnaryExpressionType.Not, new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false))).ToString());

            Assert.Equal("test(True and False, -(True and False))",new Function(new Identifier("test"), new LogicalExpression[] { new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false)), new UnaryExpression(UnaryExpressionType.Negate, new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false))) }).ToString());

            Assert.Equal("True", new ValueExpression(true).ToString());
            Assert.Equal("False", new ValueExpression(false).ToString());
            Assert.Equal("1", new ValueExpression(1).ToString());
            Assert.Equal("1.234", new ValueExpression(1.234).ToString());
            Assert.Equal("'hello'", new ValueExpression("hello").ToString());
            Assert.Equal("#" + new DateTime(2009, 1, 1) + "#", new ValueExpression(new DateTime(2009, 1, 1)).ToString());

            Assert.Equal("Sum(1 + 2)", new Function(new Identifier("Sum"), new [] { new BinaryExpression(BinaryExpressionType.Plus, new ValueExpression(1), new ValueExpression(2))}).ToString());
        }

        [Fact]
        public void ShouldHandleStringConcatenation()
        {
            Assert.Equal("toto", new Expression("'to' + 'to'").Evaluate());
            Assert.Equal("one2", new Expression("'one' + 2").Evaluate());
            Assert.Equal(3M, new Expression("1 + '2'").Evaluate());
        }

        [Fact]
        public void ShouldDetectSyntaxErrorsBeforeEvaluation()
        {
            var e = new Expression("a + b * (");
            Assert.Null(e.Error);
            Assert.True(e.HasErrors());
            Assert.True(e.HasErrors());
            Assert.NotNull(e.Error);

            e = new Expression("+ b ");
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

        private List<Exception> _exceptions;

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
                var e = new Expression(exp);
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
            Assert.Equal(1M, new Expression("aBs(-1)", EvaluateOptions.IgnoreCase).Evaluate());
            Assert.Equal(1M, new Expression("Abs(-1)", EvaluateOptions.None).Evaluate());

            try
            {
                Assert.Equal(1M, new Expression("aBs(-1)", EvaluateOptions.None).Evaluate());
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
            var e = new Expression("Round(Pow([Pi], 2) + Pow([Pi], 2) + 10, 2)");

            e.EvaluateParameter += delegate(string name, ParameterArgs arg)
            {
                if (name == "Pi")
                    arg.Result = 3.14;
            };

            e.Evaluate();
        }

        [Fact]
        public void ShouldHandleCustomFunctionsInFunctions()
        {
            var e = new Expression("if(true, func1(x) + func2(func3(y)), 0)");

            e.EvaluateFunction += delegate(string name, FunctionArgs arg)
            {
                switch (name)
                {
                    case "func1": arg.Result = 1;
                        break;
                    case "func2": arg.Result = 2 * Convert.ToDouble(arg.Parameters[0].Evaluate());
                        break;
                    case "func3": arg.Result = 3 * Convert.ToDouble(arg.Parameters[0].Evaluate());
                        break;
                }
            };

            e.EvaluateParameter += delegate(string name, ParameterArgs arg)
            {
                switch (name)
                {
                    case "x": arg.Result = 1;
                        break;
                    case "y": arg.Result = 2;
                        break;
                    case "z": arg.Result = 3;
                        break;
                }
            };

            Assert.Equal(13d, e.Evaluate());
        }


        [Fact]
        public void ShouldParseScientificNotation()
        {
            Assert.Equal(12.2d, new Expression("1.22e1").Evaluate());
            Assert.Equal(100d, new Expression("1e2").Evaluate());
            Assert.Equal(100d, new Expression("1e+2").Evaluate());
            Assert.Equal(0.01d, new Expression("1e-2").Evaluate());
            Assert.Equal(0.001d, new Expression(".1e-2").Evaluate());
            Assert.Equal(10000000000d, new Expression("1e10").Evaluate());
        }

        [Fact]
        public void ShouldEvaluateArrayParameters()
        {
            var e = new Expression("x * x", EvaluateOptions.IterateParameters);
            e.Parameters["x"] = new [] { 0, 1, 2, 3, 4 };

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
            var e = new Expression("SecretOperation(3, 6)");

            e.EvaluateFunction += delegate(string name, FunctionArgs args)
            {
                Assert.False(args.HasResult);
                if (name == "SecretOperation")
                    args.Result = null;
                Assert.True(args.HasResult);
            };

            Assert.Equal(null, e.Evaluate());
        }

        [Fact]
        public void CustomParametersShouldReturnNull()
        {
            var e = new Expression("x");

            e.EvaluateParameter += delegate(string name, ParameterArgs args)
            {
                Assert.False(args.HasResult);
                if (name == "x")
                    args.Result = null;
                Assert.True(args.HasResult);
            };

            Assert.Equal(null, e.Evaluate());
        }

        [Fact]
        public void ShouldCompareDates()
        {
            Assert.Equal(true, new Expression("#1/1/2009#==#1/1/2009#").Evaluate());
            Assert.Equal(false, new Expression("#2/1/2009#==#1/1/2009#").Evaluate());
        }

        [Fact]
        public void ShouldRoundAwayFromZero()
        {
            Assert.Equal(22d, new Expression("Round(22.5, 0)").Evaluate());
            Assert.Equal(23d, new Expression("Round(22.5, 0)", EvaluateOptions.RoundAwayFromZero).Evaluate());
        }

        [Fact]
        public void ShouldEvaluateSubExpressions()
        {
            var volume = new Expression("[surface] * h");
            var surface = new Expression("[l] * [L]");
            volume.Parameters["surface"] = surface;
            volume.Parameters["h"] = 3;
            surface.Parameters["l"] = 1;
            surface.Parameters["L"] = 2;

            Assert.Equal(6, volume.Evaluate());
        }

        [Fact]
        public void ShouldHandleLongValues()
        {
            Assert.Equal(40000000000 + 1f, new Expression("40000000000+1").Evaluate());
        }

        [Fact]
        public void ShouldCompareLongValues()
        {
            Assert.Equal(false, new Expression("(0=1500000)||(((0+2200000000)-1500000)<0)").Evaluate());
        }

        [Fact]
        public void ShouldDisplayErrorIfUncompatibleTypes()
        {
            var e = new Expression("(a > b) + 10");
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
            var expression = new Expression(formula, EvaluateOptions.BooleanCalculation) {Parameters = {["X1"] = 1}};

            expression.Evaluate().Should().Be(expectedValue);

            var lambda = expression.ToLambda<object>();
            lambda().Should().Be(expectedValue);
        }

        [Fact]
        public void ShouldNotConvertRealTypes()
        {
            var e = new Expression("x/2");
            e.Parameters["x"] = 2F;
            Assert.Equal(typeof(float), e.Evaluate().GetType());

            e = new Expression("x/2");
            e.Parameters["x"] = 2D;
            Assert.Equal(typeof(double), e.Evaluate().GetType());

            e = new Expression("x/2");
            e.Parameters["x"] = 2m;
            Assert.Equal(typeof(decimal), e.Evaluate().GetType());

            e = new Expression("a / b * 100");
            e.Parameters["a"] = 20M;
            e.Parameters["b"] = 20M;
            Assert.Equal(100M, e.Evaluate());

        }

        [Fact]
        public void ShouldShortCircuitBooleanExpressions()
        {
            var e = new Expression("([a] != 0) && ([b]/[a]>2)");
            e.Parameters["a"] = 0;

            Assert.Equal(false, e.Evaluate());
        }

        [Fact]
        public void ShouldAddDoubleAndDecimal()
        {
            var e = new Expression("1.8 + Abs([var1])");
            e.Parameters["var1"] = 9.2;

            Assert.Equal(11M, e.Evaluate());
        }
    }
}

