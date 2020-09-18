using System;
using System.Globalization;
using System.Linq;
using NCalc.Domain;
using Xunit;
using ValueType = NCalc.Domain.ValueType;

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

            public long Test(long a, long b)
            {
                return a + b;
            }

            public string Test(string a, string b)
            {
                return a + b;
            }

            public long Test(long a, long b, long c)
            {
                return a + b + c;
            }

            public string Sum(string msg, params long[] numbers)
            {
                return msg + numbers.Sum();
            }

            public long Sum(params long[] numbers)
            {
                return numbers.Sum();
            }

            public long Sum(TestObject1 obj1, TestObject2 obj2)
            {
                return obj1.Count1 + obj2.Count2;
            }

            public long Sum(TestObject2 obj1, TestObject1 obj2)
            {
                return obj1.Count2 + obj2.Count1;
            }

            public long Sum(TestObject1 obj1, TestObject1 obj2)
            {
                return obj1.Count1 + obj2.Count1;
            }

            public long Sum(TestObject2 obj1, TestObject2 obj2)
            {
                return obj1.Count2 + obj2.Count2;
            }

            public class TestObject1
            {
                public long Count1 { get; set; }
            }

            public class TestObject2
            {
                public long Count2 { get; set; }
            }


            public TestObject1 CreateTestObject1(long count)
            {
                return new TestObject1() { Count1 = count };
            }

            public TestObject2 CreateTestObject2(long count)
            {
                return new TestObject2() { Count2 = count };
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

            Assert.Equal(expected, sut());
        }

        [Fact]
        public void ShouldHandleParameters()
        {
            var expression = new Expression("[FieldA] > 5 && [FieldB] = 'test'");
            var sut = expression.ToLambda<Context, bool>();
            var context = new Context { FieldA = 7, FieldB = "test" };

            Assert.True(sut(context));
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
        public void ShouldHandleOverloadingObjectParameters()
        {
            var expression = new Expression("Sum(CreateTestObject1(2), CreateTestObject2(2)) + Sum(CreateTestObject2(1), CreateTestObject1(5))");
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
            var context = new Context();

            Assert.Equal(6L, sut(context));
        }

        [Fact]
        public void MissingMethod()
        {
            var expression = new Expression("MissingMethod(1)");

            Assert.Throws<MissingMethodException>(
                expression.ToLambda<Context, int>);
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
            Assert.Equal(-14, f());
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

        [Theory]
        [InlineData("Min(3,2)",2)]
        [InlineData("Min(3.2,6.3)", 3.2)]
        [InlineData("Max(2.6,9.6)", 9.6)]
        [InlineData("Max(9,6)", 9.0)]
        [InlineData("Pow(5,2)", 25)]
        public void ShouldHandleNumericBuiltInFunctions(string input, double expected)
        {
            var expression = new Expression(input);
            var sut = expression.ToLambda<object>();
            Assert.Equal(expected, sut());
        }

        [Theory]
        [InlineData("MyFunction()", 2.0)]
        public void ShouldHandleExternalFunctionWhenCallingToLambdaWithContext(
            string input,
            double expected)
        {
            // Arrange
            var expression = new Expression(input);
            expression.EvaluateFunction += (name, args) =>
            {
                if (name != "MyFunction")
                {
                    return;
                }

                args.Result = expected;
            };
            var sut = expression.ToLambda<object, double>();
            var context = new object();

            // Act
            var actual = sut(context);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("MyFunction(1,2)", 3L)]
        public void ShouldHandleExternalFunctionWithStaticParametersWhenCallingToLambdaWithContext(
            string input,
            long expected)
        {
            // Arrange
            var expression = new Expression(input);
            expression.EvaluateFunction += (name, args) =>
            {
                if (name != "MyFunction")
                {
                    return;
                }

                var fst = (long) args.Parameters[0].Evaluate();
                var snd = (long) args.Parameters[1].Evaluate();
                args.Result = fst + snd;
            };
            var sut = expression.ToLambda<object, long>();
            var context = new object();

            // Act
            var actual = sut(context);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("MyFunction(MyParam,2)", 3)]
        public void ShouldHandleExternalFunctionWithDynamicParametersWhenCallingToLambdaWithContext(
            string input,
            int expected)
        {
            // Arrange
            var expression = new Expression(input);
            expression.EvaluateFunction += (name, args) =>
            {
                if (name != "MyFunction")
                {
                    return;
                }

                var fst = (long) args.Parameters[0].Evaluate();
                var snd = (long) args.Parameters[1].Evaluate();
                args.Result = fst + snd;
            };
            expression.EvaluateParameter += (name, args) =>
            {
                if (name != "MyParam")
                {
                    return;
                }

                args.Result = 1L;
            };

            var sut = expression.ToLambda<object, long>();
            var context = new object();

            // Act
            var actual = sut(context);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Bar(1.23)", 1.23)]
        public void ShouldTreatFloatingPointNumbersAsDecimalWhenCallingToLambdaWithContext(
            string input,
            decimal expected)
        {
            // Arrange
            var expression = new Expression(input);

            var sut = expression.ToLambda<Foo, decimal>();
            var context = new Foo();

            // Act
            var actual = sut(context);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Baz(42)", 42L)]
        public void ShouldTreatIntegralNumbersAsLongWhenCallingToLambdaWithContext(
            string input,
            long expected)
        {
            // Arrange
            var expression = new Expression(input);

            var sut = expression.ToLambda<Foo, long>();
            var context = new Foo();

            // Act
            var actual = sut(context);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Abs(4)")]
        [InlineData("Abs(4.0)")]
        [InlineData("Acos(0.5)")]
        [InlineData("Acos(0)")]
        [InlineData("Asin(0.5)")]
        [InlineData("Asin(1)")]
        [InlineData("Atan(0.5)")]
        [InlineData("Atan(1)")]
        [InlineData("Ceiling(1.5)")]
        [InlineData("Ceiling(1)")]
        [InlineData("Cos(0.5)")]
        [InlineData("Cos(1)")]
        [InlineData("Exp(0.5)")]
        [InlineData("Exp(1)")]
        [InlineData("Floor(1.5)")]
        [InlineData("Floor(1)")]
        [InlineData("IEEERemainder(1.5, 2.3)")]
        [InlineData("IEEERemainder(1, 2)")]
        [InlineData("Log(10, 2)")]
        [InlineData("Log(1.5, 2.3)")]
        [InlineData("Log10(10)")]
        [InlineData("Log10(1.5)")]
        [InlineData("Pow(10, 2)")]
        [InlineData("Pow(1.5, 2.3)")]
        [InlineData("Round(10.3)")]
        [InlineData("Round(10.2, 2)")]
        [InlineData("Round(1.5, 2)")]
        [InlineData("Sign(2)")]
        [InlineData("Sign(1.4)")]
        [InlineData("Sin(0.5)")]
        [InlineData("Sin(1)")]
        [InlineData("Sqrt(4)")]
        [InlineData("Sqrt(4.0)")]
        [InlineData("Tan(4)")]
        [InlineData("Tan(4.0)")]
        [InlineData("Truncate(4)")]
        [InlineData("Truncate(4.0)")]
        [InlineData("Max(3, 4)")]
        [InlineData("Max(3, 4.5)")]
        [InlineData("Max(4.0, 5.5)")]
        [InlineData("Min(3, 4)")]
        [InlineData("Min(4.3, 2)")]
        [InlineData("Min(4.0, 5.5)")]
        public void ShouldBeAbleToCallMathFunctionsWhenCallingToLambdaWithContext(
            string input)
        {
            // Arrange
            var expression = new Expression(input);

            var sut = expression.ToLambda<Foo, decimal>();
            var context = new Foo();

            // Act
            var actual = sut(context);

            // Assert
            Assert.NotEqual(default, actual);
        }

        [Theory]
        [InlineData("MyFunction(MyParam + 3, 2)", 6)]
        public void ShouldHandleExternalFunctionWithDynamicExpressionWhenCallingToLambdaWithContext(
            string input,
            int expected)
        {
            // Arrange
            var expression = new Expression(input);
            expression.EvaluateFunction += (name, args) =>
            {
                if (name != "MyFunction")
                {
                    return;
                }

                var fst = (long) args.Parameters[0].Evaluate();
                var snd = (long) args.Parameters[1].Evaluate();
                args.Result = fst + snd;
            };
            expression.EvaluateParameter += (name, args) =>
            {
                if (name != "MyParam")
                {
                    return;
                }

                args.Result = 1;
            };

            var sut = expression.ToLambda<object, long>();
            var context = new object();

            // Act
            var actual = sut(context);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("MyFunction(MyFunction2() + 3, 2)", 6)]
        public void ShouldHandleExternalFunctionWithNestedExpressionWhenCallingToLambdaWithContext(
            string input,
            int expected)
        {
            // Arrange
            var expression = new Expression(input);
            expression.EvaluateFunction += (name, args) =>
            {
                if (name != "MyFunction")
                {
                    return;
                }

                var fst = (long) args.Parameters[0].Evaluate();
                var snd = (long) args.Parameters[1].Evaluate();
                args.Result = fst + snd;
            };
            expression.EvaluateFunction += (name, args) =>
            {
                if (name != "MyFunction2")
                {
                    return;
                }

                args.Result = 1;
            };

            var sut = expression.ToLambda<object, long>();
            var context = new object();

            // Act
            var actual = sut(context);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("MyFunction(MyFunction2(MyParam) + 3, 2)", 6L)]
        public void ShouldHandleExternalFunctionWithNestedExpressionWithDynamicParameterWhenCallingToLambdaWithContext(
            string input,
            long expected)
        {
            // Arrange
            var expression = new Expression(input);
            expression.EvaluateFunction += (name, args) =>
            {
                if (name != "MyFunction")
                {
                    return;
                }

                var fst = (long) args.Parameters[0].Evaluate();
                var snd = (long) args.Parameters[1].Evaluate();
                args.Result = fst + snd;
            };
            expression.EvaluateFunction += (name, args) =>
            {
                if (name != "MyFunction2")
                {
                    return;
                }

                var param = (long) args.Parameters[0].Evaluate();
                args.Result = param;
            };
            expression.EvaluateParameter += (name, args) =>
            {
                if (name != "MyParam")
                {
                    return;
                }

                args.Result = 1L;
            };

            var sut = expression.ToLambda<object, long>();
            var context = new object();

            // Act
            var actual = sut(context);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("If(false, MyDecimal, 2.5)")]
        [InlineData("If(false, 1, 2.9)")]
        [InlineData("If(true, 3.14, 2)")]
        [InlineData("If(false, 1, 2)")]
        [InlineData("If(true, MyDecimal, MyDouble)")]
        [InlineData("If(true, MyDecimal + 3.2, MyDouble)")]
        [InlineData("If(true, MyDecimal, MyDouble * 4)")]
        public void
            ShouldReturnSuccessfullyWithBranchesReturningTwoDifferentFloatingPointTypes(string s)
        {
            // Arrange
            var expression = new Expression(s);
            var context = new Foo();

            var sut = expression.ToLambda<Foo, decimal>();

            // Act
            var actual = sut(context);

            // Assert
            Assert.NotEqual(0, actual);
        }

        [Fact]
        public void ShouldAllowValueTypesAsContext()
        {
            var expression = new Expression("Foo * 2.2");
            var context = new FooStruct();

            var lambda = expression.ToLambda<FooStruct, decimal>();

            var actual = lambda(context);

            Assert.Equal(4.84m, actual);
        }

        [Fact]
        public void IfTest2()
        {
            // Arrange
            const long expected = 9999999999L;
            var expression = $"if(true, {expected}, 0)";
            var e = new Expression(expression);
            var context = new object();

            var lambda = e.ToLambda<object, long>();

            // Act
            var actual = lambda(context);

            // Assert
            Assert.Equal(expected, actual);
        }

        public class Foo
        {
            public decimal Bar(decimal d) => d;

            public long Baz(long l) => l;

            public decimal MyDecimal { get; set; } = 42.3m;

            public double MyDouble { get; set; } = 32.4;
        }

        public struct FooStruct
        {
            public decimal Foo => 2.2m;
        }
    }
}
