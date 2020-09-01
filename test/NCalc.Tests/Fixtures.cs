using System;
using NCalc.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Collections;
using System.Globalization;
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

        [Theory]
        [InlineData("2 + 3 + 5")]
        [InlineData("2 * 3 + 5")]
        [InlineData("2 * (3 + 5)")]
        [InlineData("2 * (2*(2*(2+1)))")]
        [InlineData("10 % 3")]
        [InlineData("true or false")]
        [InlineData("not true")]
        [InlineData("false || not (false and true)")]
        [InlineData("3 > 2 and 1 <= (3-2)")]
        [InlineData("3 % 2 != 10 % 3")]
        public void ExpressionShouldEvaluate(string expression)
        {
            new Expression(expression).Evaluate();
        }

        [Fact]
        public void ExpressionShouldHandleNullRightParameters()
        {
            var e = new Expression("'a string' == null", EvaluateOptions.AllowNullParameter);

            Assert.False((bool)e.Evaluate());
        }

        [Fact]
        public void ExpressionShouldHandleNullLeftParameters()
        {
            var e = new Expression("null == 'a string'", EvaluateOptions.AllowNullParameter);

            Assert.False((bool)e.Evaluate());
        }

        [Fact]
        public void ExpressionShouldHandleNullBothParameters()
        {
            var e = new Expression("null == null", EvaluateOptions.AllowNullParameter);

            Assert.True((bool)e.Evaluate());
        }

        [Fact]
        public void ShouldCompareNullToNull()
        {
            var e = new Expression(
                "[x] = null",
                EvaluateOptions.AllowNullParameter)
            {
                Parameters = { ["x"] = null }
            };


            Assert.True((bool)e.Evaluate());
        }

        [Fact]
        public void ShouldCompareNullableToNonNullable()
        {
            var e = new Expression(
                "[x] = 5",
                EvaluateOptions.AllowNullParameter)
            {
                Parameters = { ["x"] = (int?) 5 }
            };

            Assert.True((bool)e.Evaluate());

            e.Parameters["x"] = (int?)6;
            Assert.False((bool)e.Evaluate());
        }

        [Fact]
        public void ShouldCompareNullToString()
        {
            var e = new Expression(
                "[x] = 'foo'",
                EvaluateOptions.AllowNullParameter)
            {
                Parameters = { ["x"] = null }
            };

            Assert.False((bool)e.Evaluate());
        }

        [Fact]
        public void ExpressionDoesNotDefineNullParameterWithoutNullOption()
        {
            var e = new Expression("'a string' == null");

            var ex = Assert.Throws<ArgumentException>(e.Evaluate);
            Assert.Contains("Parameter 'null'", ex.Message);
        }

        [Fact]
        public void ExpressionThrowsNullReferenceExceptionWithoutNullOption()
        {
            var e = new Expression("'a string' == null")
            {
                Parameters = { ["null"] = null }
            };

            Assert.Throws<NullReferenceException>(e.Evaluate);
        }

        [Fact]
        public void ShouldParseValues()
        {
            Assert.Equal(123456L, new Expression("123456").Evaluate());
            Assert.Equal(new DateTime(2001, 01, 01), new Expression("#01/01/2001#").Evaluate());
            Assert.Equal(123.456m, new Expression("123.456").Evaluate());
            Assert.True((bool)new Expression("true").Evaluate());
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
            var e = Record.Exception(new Expression("(3 + 2").Evaluate);

            Assert.NotNull(e);
            Assert.IsType<EvaluationException>(e);
        }

        [Theory]
        [InlineData("Abs(-1)", 1d, typeof(decimal))]
        [InlineData("Acos(1)", 0d, typeof(double))]
        [InlineData("Asin(0)", 0d, typeof(double))]
        [InlineData("Atan(0)", 0d, typeof(double))]
        [InlineData("Ceiling(1.5)", 2d, typeof(double))]
        [InlineData("Cos(0)", 1d, typeof(double))]
        [InlineData("Exp(0)", 1d, typeof(double))]
        [InlineData("Floor(1.5)", 1d, typeof(double))]
        [InlineData("IEEERemainder(3,2)", -1d, typeof(double))]
        [InlineData("Log(1,10)", 0d, typeof(double))]
        [InlineData("Log10(1)", 0d, typeof(double))]
        [InlineData("Pow(3,2)", 9d, typeof(double))]
        [InlineData("Round(3.222,2)", 3.22d, typeof(double))]
        [InlineData("Sign(-10)", -1, typeof(int))]
        [InlineData("Sin(0)", 0d, typeof(double))]
        [InlineData("Sqrt(4)", 2d, typeof(double))]
        [InlineData("Tan(0)", 0d, typeof(double))]
        [InlineData("Truncate(1.7)", 1d, typeof(double))]
        public void Maths(string expression, object expected, Type type)
        {
            // HACK because attribute parameters cannot contain decimals
            if (expected.GetType() != type)
            {
                expected = Convert.ChangeType(
                    expected,
                    type,
                    CultureInfo.InvariantCulture);
            }

            Assert.Equal(expected, new Expression(expression).Evaluate());
        }

        [Fact]
        public void ExpressionShouldEvaluateCustomFunctions()
        {
            var e = new Expression("SecretOperation(3, 6)");

            e.EvaluateFunction += delegate(string name, FunctionArgs args)
                {
                    if (name == "SecretOperation")
                        args.Result = (long)args.Parameters[0].Evaluate() + (long)args.Parameters[1].Evaluate();
                };

            Assert.Equal(9L, e.Evaluate());
        }

        [Fact]
        public void ExpressionShouldEvaluateCustomFunctionsWithParameters()
        {
            var e = new Expression("SecretOperation([e], 6) + f")
            {
                Parameters =
                {
                    ["e"] = 3L,
                    ["f"] = 1L
                }
            };

            e.EvaluateFunction += delegate(string name, FunctionArgs args)
                {
                    if (name == "SecretOperation")
                        args.Result = (long)args.Parameters[0].Evaluate() + (long)args.Parameters[1].Evaluate();
                };

            Assert.Equal(10L, e.Evaluate());
        }

        [Fact]
        public void ExpressionShouldEvaluateParameters()
        {
            var e = new Expression(
                "Round(Pow(Pi, 2) + Pow([Pi Squared], 2) + [X], 2)")
            {
                Parameters =
                {
                    ["Pi Squared"] = new Expression("Pi * [Pi]"),
                    ["X"] = 10
                }
            };

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
            var eif =
                new Expression("if([divider] <> 0, [divided] / [divider], 0)")
                {
                    Parameters =
                    {
                        ["divider"] = 5,
                        ["divided"] = 5
                    }
                };

            Assert.Equal(1d, eif.Evaluate());

            eif = new Expression("if([divider] <> 0, [divided] / [divider], 0)")
            {
                Parameters =
                {
                    ["divider"] = 0,
                    ["divided"] = 5
                }
            };
            Assert.Equal(0L, eif.Evaluate());
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
            var ein = new Expression("in((2 + 2), [1], [2], 1 + 2, 4, 1 / 0)")
            {
                Parameters =
                {
                    ["1"] = 2,
                    ["2"] = 5
                }
            };

            var einActual = ein.Evaluate();
            Assert.IsType<bool>(einActual);
            Assert.True((bool)einActual);

            var eout = new Expression("in((2 + 2), [1], [2], 1 + 2, 3)")
            {
                Parameters =
                {
                    ["1"] = 2,
                    ["2"] = 5
                }
            };

            var eoutActual = eout.Evaluate();
            Assert.IsType<bool>(eoutActual);
            Assert.False((bool)eoutActual);

            // Should work with strings
            var estring = new Expression("in('to' + 'to', 'titi', 'toto')");

            var estringActual = estring.Evaluate();
            Assert.IsType<bool>(estringActual);
            Assert.True((bool)estringActual);
        }

        [Theory]
        [InlineData("!true", false)]
        [InlineData("not false", true)]
        [InlineData("2 * 3", 6L)]
        [InlineData("6 / 2", 3d)]
        [InlineData("7 % 2", 1L)]
        [InlineData("2 + 3", 5L)]
        [InlineData("2 - 1", 1L)]
        [InlineData("1 < 2", true)]
        [InlineData("1 > 2", false)]
        [InlineData("1 <= 2", true)]
        [InlineData("1 <= 1", true)]
        [InlineData("1 >= 2", false)]
        [InlineData("1 >= 1", true)]
        [InlineData("1 = 1", true)]
        [InlineData("1 == 1", true)]
        [InlineData("1 != 1", false)]
        [InlineData("1 <> 1", false)]
        [InlineData("1 & 1", 1)]
        [InlineData("1 | 1", 1)]
        [InlineData("1 ^ 1", 0)]
        [InlineData("~1", ~1)]
        [InlineData("2 >> 1", 1)]
        [InlineData("2 << 1", 4)]
        [InlineData("true && false", false)]
        [InlineData("true and false", false)]
        [InlineData("true || false", true)]
        [InlineData("true or false", true)]
        [InlineData("if(true, 0, 1)", 0L)]
        [InlineData("if(false, 0, 1)", 1L)]
        public void ShouldEvaluateOperators(string expression, object expected)
        {
            var actual = new Expression(expression).Evaluate();

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("2+2+2+2", 8L)]
        [InlineData("2*2*2*2", 16L)]
        [InlineData("2*2+2", 6L)]
        [InlineData("2+2*2", 6L)]
        [InlineData("1 + 2 + 3 * 4 / 2", 9d)]
        [InlineData("18/2/2*3", 13.5)]
        public void ShouldHandleOperatorsPriority(string expression, object expected)
        {
            var actual = new Expression(expression).Evaluate();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldNotLoosePrecision()
        {
            Assert.Equal(0.5, new Expression("3/6").Evaluate());
        }

        [Fact]
        public void ShouldThrowAnExpcetionWhenInvalidNumber()
        {
            Exception e = Record.Exception(new Expression("4. + 2").Evaluate);

            Assert.NotNull(e);
            Assert.IsType<EvaluationException>(e);
        }

        [Fact]
        public void ShouldNotRoundDecimalValues()
        {
            var actual = new Expression("0 <= -0.6").Evaluate();

            Assert.IsType<bool>(actual);
            Assert.False((bool)actual);
        }

        [Fact]
        public void ShouldEvaluateTernaryExpression()
        {
            Assert.Equal(1L, new Expression("1+2<3 ? 3+4 : 1").Evaluate());
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
                long n1 = r1.Next(10);
                long n2 = r2.Next(10);

                // Constructs a simple addition randomly. Odds are that the same expression gets constructed multiple times by different threads
                var exp = n1 + " + " + n2;
                var e = new Expression(exp);
                Assert.Equal(n1 + n2, e.Evaluate());
            }
            catch (Exception e)
            {
                _exceptions.Add(e);
            }
        }

        [Theory]
        [InlineData("aBs(-1)", "IgnoreCase", 1)]
        [InlineData("Abs(-1)", "None", 1)]
        public void ShouldHandleCaseSensitiveness(string expression, string option, object expected)
        {
            // Arrange
            var evaluateOptions = (EvaluateOptions) Enum.Parse(
                typeof(EvaluateOptions),
                option);
            expected = Convert.ToDecimal(expected);

            // Act
            var actual = new Expression(expression, evaluateOptions).Evaluate();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldThrowWhenEvaluateOptionsIsCaseSensitive()
        {
            Assert.Throws<ArgumentException>(new Expression("aBs(-1)", EvaluateOptions.None).Evaluate);
        }

        [Fact]
        public void ShouldHandleCustomParametersWhenNoSpecificParameterIsDefined()
        {
            var e = new Expression("Round(Pow([Pi], 2) + Pow([Pi], 2) + 10, 2)");

            e.EvaluateParameter += (name, arg) =>
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

            e.EvaluateFunction += (name, arg) =>
            {
                switch (name)
                {
                    case "func1": arg.Result = 1;
                        break;
                    case "func2":
                        arg.Result =
                            2 * Convert.ToDouble(arg.Parameters[0].Evaluate());
                        break;
                    case "func3":
                        arg.Result =
                            3 * Convert.ToDouble(arg.Parameters[0].Evaluate());
                        break;
                }
            };

            e.EvaluateParameter += (name, arg) =>
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

        [Theory]
        [InlineData(12.2d, "1.22e1")]
        [InlineData(100d, "1e2")]
        [InlineData(100d, "1e+2")]
        [InlineData(0.01d, "1e-2")]
        [InlineData(0.001d, ".1e-2")]
        [InlineData(10000000000d, "1e10")]
        public void ShouldParseScientificNotation(object expected, string expression)
        {
            expected = Convert.ToDecimal(expected);
            var actual = new Expression(expression).Evaluate();

            Assert.IsType<decimal>(actual);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldEvaluateArrayParameters()
        {
            var e = new Expression("x * x", EvaluateOptions.IterateParameters)
            {
                Parameters = { ["x"] = new[] { 0, 1, 2, 3, 4 } }
            };

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

            Assert.Null(e.Evaluate());
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

            Assert.Null(e.Evaluate());
        }

        [Theory]
        [InlineData(true, "#1/1/2009#==#1/1/2009#")]
        [InlineData(false, "#2/1/2009#==#1/1/2009#")]
        public void ShouldCompareDates(bool expected, string expression)
        {
            Assert.Equal(expected, new Expression(expression).Evaluate());
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
            Assert.Equal(40000000000 + 1m, new Expression("40000000000+1").Evaluate());
        }

        [Fact]
        public void ShouldCompareLongValues()
        {
            var actual = new Expression("(0=1500000)||(((0+2200000000)-1500000)<0)").Evaluate();

            Assert.IsType<bool>(actual);
            Assert.False((bool)actual);
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
            var e = new Expression("x/2") { Parameters = { ["x"] = 2F } };
            Assert.Equal(typeof(float), e.Evaluate().GetType());

            e = new Expression("x/2") { Parameters = { ["x"] = 2D } };
            Assert.Equal(typeof(double), e.Evaluate().GetType());

            e = new Expression("x/2") { Parameters = { ["x"] = 2m } };
            Assert.Equal(typeof(decimal), e.Evaluate().GetType());

            e = new Expression("a / b * 100")
            {
                Parameters =
                {
                    ["a"] = 20M,
                    ["b"] = 20M
                }
            };
            Assert.Equal(100M, e.Evaluate());

        }

        [Fact]
        public void ShouldShortCircuitBooleanExpressions()
        {
            var e = new Expression("([a] != 0) && ([b]/[a]>2)")
            {
                Parameters = { ["a"] = 0 }
            };

            var actual = e.Evaluate();

            Assert.IsType<bool>(actual);
            Assert.False((bool)actual);
        }

        [Fact]
        public void ShouldAddDoubleAndDecimal()
        {
            var e = new Expression("1.8 + Abs([var1])")
            {
                Parameters = { ["var1"] = 9.2 }
            };

            Assert.Equal(11M, e.Evaluate());
        }

        [Fact]
        public void ShouldSubtractDoubleAndDecimal()
        {
            var e = new Expression("[double] - [decimal]")
            {
                Parameters =
                {
                    ["double"] = 2D,
                    ["decimal"] = 2m
                }
            };

            Assert.Equal(0m, e.Evaluate());
        }

        [Fact]
        public void ShouldMultiplyDoubleAndDecimal()
        {
            var e = new Expression("[double] * [decimal]")
            {
                Parameters =
                {
                    ["double"] = 2D,
                    ["decimal"] = 2m
                }
            };

            Assert.Equal(4m, e.Evaluate());
        }

        [Fact]
        public void ShouldDivideDoubleAndDecimal()
        {
            var e = new Expression("[double] / [decimal]")
            {
                Parameters =
                {
                    ["double"] = 2D,
                    ["decimal"] = 2m
                }
            };

            Assert.Equal(1m, e.Evaluate());
        }

        [Fact]
        public void ShouldModDoubleAndDecimal()
        {
            var e = new Expression("[double] % [decimal]")
            {
                Parameters =
                {
                    ["double"] = 2D,
                    ["decimal"] = 2m
                }
            };

            Assert.Equal(0m, e.Evaluate());
        }

        [InlineData("Min(2,1.97)", 1.97)]
        [InlineData("Max(2,2.33)", 2.33)]
        [Theory]
        public void ShouldCheckPrecisionOfBothParametersForMaxAndMin(
            string expression,
            object expected)
        {
            expected = Convert.ToDecimal(expected);
            var e = new Expression(expression);

            var result = e.Evaluate();

            Assert.Equal(expected, result);
        }
    }
}

