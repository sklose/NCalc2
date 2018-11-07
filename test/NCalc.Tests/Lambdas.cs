using System;
using System.Threading.Tasks;
using Xunit;

namespace NCalc.Tests
{
    public class Lambdas
    {
        public class Context
        {
            public int FieldA { get; set; }
            public string FieldB { get; set; }
            public decimal FieldC { get; set; }
            public decimal? FieldD { get; set; }
            public int? FieldE { get; set; }

            public int Test(int a, int b)
            {
                return a + b;
            }
            
            public string Test(string a, string b) 
            {
                return a + b;
            }

            public async Task<int> TestAsync(int a, int b)
            {
                await Task.Delay(1);
                return a + b;
            }

            public int Test(int a, int b, int c) 
            {
                return a + b + c;
            }

            public string Sum(string msg, params int[] numbers) {
                int total = 0;
                foreach (var num in numbers) {
                    total += num;
                }
                return msg + total;
            }

            public int Sum(params int[] numbers) 
            {
                int total = 0;
                foreach (var num in numbers) {
                    total += num;
                }
                return total;
            }

            public async Task<decimal> TestDecimalAsync(int a, int b)
            {
                await Task.Delay(1);
                return a + b;
            }
        }

        [Theory]
        [InlineData("1+2", 3)]
        [InlineData("1-2", -1)]
        [InlineData("2*2", 4)]
        [InlineData("10/2", 5)]
        [InlineData("7%2", 1)]
        public void ShouldHandleIntegers(string input, int expected)
        {
            var expression = new Expression(input);
            var sut = expression.ToLambda<int>();

            Assert.Equal(sut(), expected);
        }

        [Fact]
        public void ShouldHandleParameters()
        {
            var expression = new Expression("[FieldA] > 5 && [FieldB] = 'test'");
            var sut = expression.ToLambda<Context, bool>();
            var context = new Context {FieldA = 7, FieldB = "test"};

            Assert.True(sut(context));
        }

        public async void ShouldHandleAsyncCustomInlineFunctions()
        {
            var e = new Expression("SecretOperation(3, 6)");

            e.EvaluateFunctionAsync += async delegate (string name, FunctionArgs args)
            {
                await Task.Delay(1);
                if (name == "SecretOperation")
                    args.Result = (int)await args.Parameters[0].EvaluateAsync() + (int)await args.Parameters[1].EvaluateAsync();
            };

            var sut = await e.ToLambdaAsync<int>();
            Assert.Equal(9, sut());

        }


        [Fact]
        public void ShouldHandleOverloadingSameParamCount() 
        {
            var expression = new Expression("Test('Hello', ' world!')");
            var sut = expression.ToLambda<Context, string>();
            var context = new Context();

            Assert.Equal("Hello world!", sut(context));
        }

        [Fact]
        public void ShouldHandleOverloadingDifferentParamCount() 
        {
            var expression = new Expression("Test(Test(1, 2), 3, 4)");
            var sut = expression.ToLambda<Context, int>();
            var context = new Context();

            Assert.Equal(10, sut(context));
        }

        [Fact]
        public void ShouldHandleParamsKeyword() 
        {
            var expression = new Expression("Sum(Test(1,1),2)");
            var sut = expression.ToLambda<Context, int>();
            var context = new Context();

            Assert.Equal(4, sut(context));
        }

        [Fact]
        public void ShouldHandleMixedParamsKeyword() {
            var expression = new Expression("Sum('Your total is: ', Test(1,1), 2, 3)");
            var sut = expression.ToLambda<Context, string>();
            var context = new Context();

            Assert.Equal("Your total is: 7", sut(context));
        }

        [Fact]
        public void ShouldHandleCustomFunctions()
        {
            var expression = new Expression("Test(Test(1, 2), 3)");
            var sut = expression.ToLambda<Context, int>();
            var context = new Context() { FieldA=1 };

            Assert.Equal(sut(context), 6);
        }


        [Fact]
        public async void ShouldHandleCustomAsyncFunctions()
        {
            var expression = new Expression("TestDecimalAsync(TestAsync(1, 2), 3)");
            var sut = await expression.ToLambdaAsync<Context, int>();
            var context = new Context() { FieldA = 1};

            Assert.Equal(sut(context), 6);
        }

        [Fact]
        public void MissingMethod()
        {
            var expression = new Expression("MissingMethod(1)");
            try
            {
                var sut = expression.ToLambda<Context, int>();
            }
            catch(System.MissingMethodException ex)
            {

                System.Diagnostics.Debug.Write(ex);
                Assert.True(true);
                return;
            }
            Assert.True(false);

        }

        [Fact]
        public void ShouldHandleTernaryOperator()
        {
            var expression = new Expression("Test(1, 2) = 3 ? 1 : 2");
            var sut = expression.ToLambda<Context, int>();
            var context = new Context();

            Assert.Equal(sut(context), 1);
        }

        [Fact]
        public void Issue1()
        {
            var expr = new Expression("2 + 2 - a - b - x");

            decimal x = 5m;
            decimal a = 6m;
            decimal b = 7m;

            expr.Parameters["x"] = x;
            expr.Parameters["a"] = a;
            expr.Parameters["b"] = b;

            var f = expr.ToLambda<float>(); // Here it throws System.ArgumentNullException. Parameter name: expression
            Assert.Equal(f(), -14);
        }

        [Theory]
        [InlineData("if(true, true, false)")]
        [InlineData("in(3, 1, 2, 3, 4)")]
        public void ShouldHandleBuiltInFunctions(string input)
        {
            var expression = new Expression(input);
            var sut = expression.ToLambda<bool>();
            Assert.True(sut());
        }

        [Theory]
        [InlineData("[FieldA] > [FieldC]", true)]
        [InlineData("[FieldC] > 1.34", true)]
        [InlineData("[FieldC] > (1.34 * 2) % 3", false)]
        [InlineData("[FieldE] = 2", true)]
        [InlineData("[FieldD] > 0", false)]
        public void ShouldHandleDataConversions(string input, bool expected)
        {
            var expression = new Expression(input);
            var sut = expression.ToLambda<Context, bool>();
            var context = new Context { FieldA = 7, FieldB = "test", FieldC = 2.4m, FieldE = 2 };

            Assert.Equal(expected, sut(context));
        }
    }
}
