using System;
using NCalc.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Collections;
using Xunit;
using System.Threading.Tasks;

namespace NCalc.Tests
{
    public class FixturesAsync
    {
        [Fact]
        public async void ExpressionShouldEvaluate()
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
                Console.WriteLine("{0} = {1}",
                    expression,
                    await new Expression(expression).EvaluateAsync());
        }

        [Fact]
        public async void ShouldParseValues()
        {
            Assert.Equal(123456, await new Expression("123456").EvaluateAsync());
            Assert.Equal(new DateTime(2001, 01, 01),await  new Expression("#01/01/2001#").EvaluateAsync());
            Assert.Equal(123.456d, await new Expression("123.456").EvaluateAsync());
            Assert.True((bool) await new Expression("true").EvaluateAsync());
            Assert.Equal("true", await new Expression("'true'").EvaluateAsync());
            Assert.Equal("azerty", await new Expression("'azerty'").EvaluateAsync());
        }

        [Fact]
        public async void ParsedExpressionToStringShouldHandleSmallDecimals()
        {
            // small decimals starting with 0 resulting in scientific notation did not work in original NCalc
            var equation = "0.000001";
            var testExpression = new Expression(equation);
            await testExpression.EvaluateAsync();
            Assert.Equal(equation, testExpression.ParsedExpression.ToString());
        }

        [Fact]
        public async void ShouldHandleUnicode()
        {
            Assert.Equal("経済協力開発機構",await new Expression("'経済協力開発機構'").EvaluateAsync());
            Assert.Equal("Hello", await new Expression(@"'\u0048\u0065\u006C\u006C\u006F'").EvaluateAsync());
            Assert.Equal("だ", await new Expression(@"'\u3060'").EvaluateAsync());
            Assert.Equal("\u0100", await new Expression(@"'\u0100'").EvaluateAsync());
        }

        [Fact]
        public async void ShouldEscapeCharacters()
        {
            Assert.Equal("'hello'", await new Expression(@"'\'hello\''").EvaluateAsync());
            Assert.Equal(" ' hel lo ' ", await new Expression(@"' \' hel lo \' '").EvaluateAsync());
            Assert.Equal("hel\nlo", await new Expression(@"'hel\nlo'").EvaluateAsync());
        }

        [Fact]
        public async void ShouldDisplayErrorMessages()
        {
            try
            {
                await new Expression("(3 + 2").EvaluateAsync();
                throw new Exception();
            }
            catch(EvaluationException e)
            {
                Console.WriteLine("Error catched: " + e.Message);
            }
        }

        [Fact]
        public async void Maths()
        {
            Assert.Equal(1M, await new Expression("Abs(-1)").EvaluateAsync());
            Assert.Equal(0d, await new Expression("Acos(1)").EvaluateAsync());
            Assert.Equal(0d, await new Expression("Asin(0)").EvaluateAsync());
            Assert.Equal(0d, await new Expression("Atan(0)").EvaluateAsync());
            Assert.Equal(2d, await new Expression("Ceiling(1.5)").EvaluateAsync());
            Assert.Equal(1d, await new Expression("Cos(0)").EvaluateAsync());
            Assert.Equal(1d, await new Expression("Exp(0)").EvaluateAsync());
            Assert.Equal(1d, await new Expression("Floor(1.5)").EvaluateAsync());
            Assert.Equal(-1d, await new Expression("IEEERemainder(3,2)").EvaluateAsync());
            Assert.Equal(0d, await new Expression("Log(1,10)").EvaluateAsync());
            Assert.Equal(0d, await new Expression("Log10(1)").EvaluateAsync());
            Assert.Equal(9d, await new Expression("Pow(3,2)").EvaluateAsync());
            Assert.Equal(3.22d, await new Expression("Round(3.222,2)").EvaluateAsync());
            Assert.Equal(-1, await new Expression("Sign(-10)").EvaluateAsync());
            Assert.Equal(0d, await new Expression("Sin(0)").EvaluateAsync());
            Assert.Equal(2d, await new Expression("Sqrt(4)").EvaluateAsync());
            Assert.Equal(0d, await new Expression("Tan(0)").EvaluateAsync());
            Assert.Equal(1d, await new Expression("Truncate(1.7)").EvaluateAsync());
        }

        [Fact]
        public async void ExpressionShouldEvaluateCustomFunctions()
        {
            var e = new Expression("SecretOperation(3, 6)");

            e.EvaluateFunctionAsync += async delegate(string name, FunctionArgs args)
                {
                    if (name == "SecretOperation")
                        args.Result = (int)await args.Parameters[0].EvaluateAsync() + (int)await args.Parameters[1].EvaluateAsync();
                };

            Assert.Equal(9, await e.EvaluateAsync());
        }

        [Fact]
        public async void ExpressionShouldEvaluateCustomFunctionsWithParameters()
        {
            var e = new Expression("SecretOperation([e], 6) + f");
            e.Parameters["e"] = 3;
            e.Parameters["f"] = 1;

            e.EvaluateFunctionAsync += async delegate(string name, FunctionArgs args)
                {
                    if (name == "SecretOperation")
                        args.Result = (int)await args.Parameters[0].EvaluateAsync() + (int) await args.Parameters[1].EvaluateAsync();
                };

            Assert.Equal(10, await e.EvaluateAsync());
        }

        [Fact]
		public async void ExpressionShouldEvaluateParameters()
		{
			var e = new Expression("Round(Pow(Pi, 2) + Pow([Pi Squared], 2) + [X], 2)");
		    
			e.Parameters["Pi Squared"] = new Expression("Pi * [Pi]");
			e.Parameters["X"] = 10;

			e.EvaluateParameterAsync += async delegate(string name, ParameterArgs args)
				{
					if (name == "Pi")
						args.Result = 3.14;
				};

			Assert.Equal(117.07, await e.EvaluateAsync());
		}

        [Fact]
        public async void ShouldEvaluateConditionnal()
        {
            var eif = new Expression("if([divider] <> 0, [divided] / [divider], 0)");
            eif.Parameters["divider"] = 5;
            eif.Parameters["divided"] = 5;

            Assert.Equal(1d, await eif.EvaluateAsync());

            eif = new Expression("if([divider] <> 0, [divided] / [divider], 0)");
            eif.Parameters["divider"] = 0;
            eif.Parameters["divided"] = 5;
            Assert.Equal(0, await eif.EvaluateAsync());
        }

        [Fact]
        public async void ShouldOverrideExistingFunctions()
        {
            var e = new Expression("Round(1.99, 2)");

            Assert.Equal(1.99d, await e.EvaluateAsync());

            e.EvaluateFunctionAsync += async delegate(string name, FunctionArgs args)
            {
                if (name == "Round")
                    args.Result = 3;
            };

            Assert.Equal(3, await e.EvaluateAsync());
        }

        [Fact]
        public async void ShouldEvaluateInOperator()
        {
            // The last argument should not be evaluated
            var ein = new Expression("in((2 + 2), [1], [2], 1 + 2, 4, 1 / 0)");
            ein.Parameters["1"] = 2;
            ein.Parameters["2"] = 5;

            Assert.Equal(true, await ein.EvaluateAsync());

            var eout = new Expression("in((2 + 2), [1], [2], 1 + 2, 3)");
            eout.Parameters["1"] = 2;
            eout.Parameters["2"] = 5;

            Assert.Equal(false,await eout.EvaluateAsync());

            // Should work with strings
            var estring = new Expression("in('to' + 'to', 'titi', 'toto')");

            Assert.Equal(true, await estring.EvaluateAsync());

        }

        [Fact]
        public async void ShouldEvaluateOperators()
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
                Assert.Equal(pair.Value, await new Expression(pair.Key).EvaluateAsync());
            }
            
        }

        [Fact]
        public async void ShouldHandleOperatorsPriority()
        {
            Assert.Equal(8, await new Expression("2+2+2+2").EvaluateAsync());
            Assert.Equal(16, await new Expression("2*2*2*2").EvaluateAsync());
            Assert.Equal(6, await new Expression("2*2+2").EvaluateAsync());
            Assert.Equal(6, await new Expression("2+2*2").EvaluateAsync());

            Assert.Equal(9d, await new Expression("1 + 2 + 3 * 4 / 2").EvaluateAsync());
            Assert.Equal(13.5, await new Expression("18/2/2*3").EvaluateAsync());
        }

        [Fact]
        public async void ShouldNotLoosePrecision()
        {
            Assert.Equal(0.5, await new Expression("3/6").EvaluateAsync());
        }

        [Fact]
        public async void ShouldThrowAnExpcetionWhenInvalidNumber()
        {
            try
            {
                await new Expression("4. + 2").EvaluateAsync();
                throw new Exception();
            }
            catch (EvaluationException e)
            {
                Console.WriteLine("Error catched: " + e.Message);
            }
        }

        [Fact]
        public async void ShouldNotRoundDecimalValues()
        {
            Assert.Equal(false, await new Expression("0 <= -0.6").EvaluateAsync());
        }

        [Fact]
        public async void ShouldEvaluateTernaryExpression()
        {
            Assert.Equal(1, await new Expression("1+2<3 ? 3+4 : 1").EvaluateAsync());
        }

        [Fact]
        public async void ShouldSerializeExpression()
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
        public async void ShouldHandleStringConcatenation()
        {
            Assert.Equal("toto", await new Expression("'to' + 'to'").EvaluateAsync());
            Assert.Equal("one2", await new Expression("'one' + 2").EvaluateAsync());
            Assert.Equal(3M, await new Expression("1 + '2'").EvaluateAsync());
        }

        [Fact]
        public async void ShouldDetectSyntaxErrorsBeforeEvaluation()
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
        public async void ShouldReuseCompiledExpressionsInMultiThreadedMode()
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
                    Console.WriteLine(_exceptions[0].StackTrace);
                    throw _exceptions[0];
                }
            }
        }

        private List<Exception> _exceptions;

        private async void WorkerThread()
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
                Assert.True((await e.EvaluateAsync()).Equals(n1 + n2));
            }
            catch (Exception e)
            {
                _exceptions.Add(e);
            }
        }

        [Fact]
        public async void ShouldHandleCaseSensitiveness()
        {
            Assert.Equal(1M, await new Expression("aBs(-1)", EvaluateOptions.IgnoreCase).EvaluateAsync());
            Assert.Equal(1M, await new Expression("Abs(-1)", EvaluateOptions.None).EvaluateAsync());

            try
            {
                Assert.Equal(1M, await new Expression("aBs(-1)", EvaluateOptions.None).EvaluateAsync());
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
        public async void ShouldHandleCustomParametersWhenNoSpecificParameterIsDefined()
        {
            var e = new Expression("Round(Pow([Pi], 2) + Pow([Pi], 2) + 10, 2)");

            e.EvaluateParameterAsync += async delegate(string name, ParameterArgs arg)
            {
                if (name == "Pi")
                    arg.Result = 3.14;
            };

            await e.EvaluateAsync();
        }

        [Fact]
        public async void ShouldHandleCustomFunctionsInFunctions()
        {
            var e = new Expression("if(true, func1(x) + func2(func3(y)), 0)");

            e.EvaluateFunctionAsync += async delegate(string name, FunctionArgs arg)
            {
                switch (name)
                {
                    case "func1": arg.Result = 1;
                        break;
                    case "func2": arg.Result = 2 * Convert.ToDouble(await arg.Parameters[0].EvaluateAsync());
                        break;
                    case "func3": arg.Result = 3 * Convert.ToDouble(await arg.Parameters[0].EvaluateAsync());
                        break;
                }
            };

            e.EvaluateParameterAsync += async delegate(string name, ParameterArgs arg)
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

            Assert.Equal(13d, await e.EvaluateAsync());
        }


        [Fact]
        public async void ShouldParseScientificNotation()
        {
            Assert.Equal(12.2d, await new Expression("1.22e1").EvaluateAsync());
            Assert.Equal(100d, await new Expression("1e2").EvaluateAsync());
            Assert.Equal(100d, await new Expression("1e+2").EvaluateAsync());
            Assert.Equal(0.01d, await new Expression("1e-2").EvaluateAsync());
            Assert.Equal(0.001d, await new Expression(".1e-2").EvaluateAsync());
            Assert.Equal(10000000000d, await new Expression("1e10").EvaluateAsync());
        }

        [Fact]
        public async void ShouldEvaluateArrayParameters()
        {
            var e = new Expression("x * x", EvaluateOptions.IterateParameters);
            e.Parameters["x"] = new [] { 0, 1, 2, 3, 4 };

            var result = (IList)await e.EvaluateAsync();

            Assert.Equal(0, result[0]);
            Assert.Equal(1, result[1]);
            Assert.Equal(4, result[2]);
            Assert.Equal(9, result[3]);
            Assert.Equal(16, result[4]);
        }

        [Fact]
        public async void CustomFunctionShouldReturnNull()
        {
            var e = new Expression("SecretOperation(3, 6)");

            e.EvaluateFunctionAsync += async delegate(string name, FunctionArgs args)
            {

                await Task.Delay(10);
                Assert.False(args.HasResult);
                if (name == "SecretOperation")
                    args.Result = null;
                Assert.True(args.HasResult);
            };

            Assert.Null(await e.EvaluateAsync());
        }

        [Fact]
        public async void CustomParametersShouldReturnNull()
        {
            var e = new Expression("x");

            e.EvaluateParameterAsync += async delegate(string name, ParameterArgs args)
            {
                Assert.False(args.HasResult);
                if (name == "x")
                    args.Result = null;
                Assert.True(args.HasResult);
            };

            Assert.Null(await e.EvaluateAsync());
        }

        [Fact]
        public async void ShouldCompareDates()
        {
            Assert.True( (bool)await new Expression("#1/1/2009#==#1/1/2009#").EvaluateAsync());
            Assert.False((bool) await new Expression("#2/1/2009#==#1/1/2009#").EvaluateAsync());
        }

        [Fact]
        public async void ShouldRoundAwayFromZero()
        {
            Assert.Equal(22d, await new Expression("Round(22.5, 0)").EvaluateAsync());
            Assert.Equal(23d, await new Expression("Round(22.5, 0)", EvaluateOptions.RoundAwayFromZero).EvaluateAsync());
        }

        [Fact]
        public async void ShouldEvaluateSubExpressions()
        {
            var volume = new Expression("[surface] * h");
            var surface = new Expression("[l] * [L]");
            volume.Parameters["surface"] = surface;
            volume.Parameters["h"] = 3;
            surface.Parameters["l"] = 1;
            surface.Parameters["L"] = 2;

            Assert.Equal(6, await volume.EvaluateAsync());
        }

        [Fact]
        public async void ShouldHandleLongValues()
        {
            Assert.Equal(40000000000 + 1f, await new Expression("40000000000+1").EvaluateAsync());
        }

        [Fact]
        public async void ShouldCompareLongValues()
        {
            Assert.Equal(false, await new Expression("(0=1500000)||(((0+2200000000)-1500000)<0)").EvaluateAsync());
        }

        [Fact]
        public async void ShouldDisplayErrorIfUncompatibleTypes()
        {
            var e = new Expression("(a > b) + 10");
            e.Parameters["a"] = 1;
            e.Parameters["b"] = 2;
            await Assert.ThrowsAsync<InvalidOperationException>(async() => await e.EvaluateAsync());
        }

        [Fact]
        public async void ShouldNotConvertRealTypes() 
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
            Assert.Equal(100M, await e.EvaluateAsync());

        }

        [Fact]
        public async void ShouldShortCircuitBooleanExpressions()
        {
            var e = new Expression("([a] != 0) && ([b]/[a]>2)");
            e.Parameters["a"] = 0;

            Assert.Equal(false, await e.EvaluateAsync());
        }

        [Fact]
        public async void ShouldAddDoubleAndDecimal()
        {
            var e = new Expression("1.8 + Abs([var1])");
            e.Parameters["var1"] = 9.2;

            Assert.Equal(11M, await e.EvaluateAsync());
        }
    }
}

