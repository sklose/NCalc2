using System;
using FluentAssertions;
using Xunit;
using LQ = System.Linq.Expressions;

namespace NCalc.Tests
{
    public class Lambdas
    {
        private class Context
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
        }

        [Fact]
        public void ShouldHandleParameters()
        {
            var expression = new Expression("[FieldA] > 5 && [FieldB] = 'test'");
            var sut = expression.ToLambda<Context, bool>();
            var context = new Context {FieldA = 7, FieldB = "test"};

            Assert.True(sut(context));
        }

        [Fact]
        public void ThrowUnknownParameter()
        {
            var expression = new Expression("3*PI");
            var action = new Action(() => expression.ToLambda<double>());
            action.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ShouldHandleCustomFunctions()
        {
            var expression = new Expression("Test(Test(1, 2), 3)");
            var sut = expression.ToLambda<Context, int>();
            var context = new Context();

            Assert.Equal(6, sut(context));
        }

        [Fact]
        public void MissingMethod()
        {
            var expression = new Expression("MissingMethod(1)");
            try
            {
                var sut = expression.ToLambda<Context, int>();
            }
            catch (System.MissingMethodException ex)
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

            Assert.Equal(1, sut(context));
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

        [Fact]
        public void ShouldHandleExtendedParameters()
        {
            var expression = new Expression("PI");
            expression.EvaluateParameterExpression += (sender, args) =>
            {
                if (args.Name == "PI")
                {
                    args.Result = LQ.Expression.Constant(3.1415);
                }
            };

            var sut = expression.ToLambda<double>();
            sut().Should().BeApproximately(3.1415, 0.001);
        }

        [Fact]
        public void ShouldHandleExtendedFunctions()
        {
            var expression = new Expression("MyFunc(X1)") {Parameters = {["X1"] = 1}};
            expression.EvaluateFunctionExpression += (sender, args) =>
            {
                if (args.Name == "MyFunc")
                {
                    args.Result = LQ.Expression.Add(args.ArgumentExpressions[0], LQ.Expression.Constant(1));
                }
            };

            var sut = expression.ToLambda<int>();
            sut().Should().Be(2);
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
