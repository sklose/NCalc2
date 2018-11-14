using System;
using System.Diagnostics;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace NCalc.Tests
{
    public class Performance
    {
        private const int Iterations = 100000;

        private readonly ITestOutputHelper _output;

        public Performance(ITestOutputHelper output)
        {
            _output = output;
        }

        private class Context
        {
            public int Param1 { get; set; }
            public int Param2 { get; set; }

            public int Foo(int a, int b)
            {
                return Math.Min(a, b);
            }
        }

        [Theory]
        [InlineData("(4 * 12 / 7) + ((9 * 2) % 8)")]
        [InlineData("5 * 2 = 2 * 5 && (1 / 3.0) * 3 = 1")]
        public void Arithmetics(string formula)
        {
            var expression = new Expression(formula);
            var lambda = expression.ToLambda<object>();

            var m1 = Measure(i => expression.Evaluate());
            var m2 = Measure(i => lambda());

            PrintResult(formula, m1, m2);
        }

        [Theory]
        [InlineData("[Param1] * 7 + [Param2]")]
        public void ParameterAccess(string formula)
        {
            var expression = new Expression(formula);
            expression.AddParameter("Param1", 4);
            expression.AddParameter("Param2", 9);
            var lambda = expression.ToLambda<int>();

            var m1 = Measure(i => expression.Evaluate());
            var m2 = Measure(i => lambda());

            PrintResult(formula, m1, m2);
        }

        [Theory]
        [InlineData("[Param1] * 7 + [Param2]")]
        public void DynamicParameterAccess(string formula)
        {
            var expression = new Expression(formula);
            var index = expression.AddParameter("Param1", 4);
            expression.AddParameter("Param2", 9);
            var lambda = expression.ToLambda<int>();

            var m1 = Measure(i =>
            {
                expression.SetParameter(index, i);
                expression.Evaluate();
            });
            var m2 = Measure(i =>
            {
                expression.SetParameter(index, i);
                lambda();
            });

            PrintResult(formula, m1, m2);
        }

        [Theory]
        [InlineData("[Param1] * 7 + [Param2]")]
        public void ContextAccess(string formula)
        {
            var expression = new Expression(formula);
            var lambda = expression.ToLambda<Context, int>();

            var context = new Context {Param1 = 4, Param2 = 9};
            expression.AddParameter("Param1", 4);
            expression.AddParameter("Param2", 9);

            var m1 = Measure(i => expression.Evaluate());
            var m2 = Measure(i => lambda(context));

            PrintResult(formula, m1, m2);
        }

        [Theory]
        [InlineData("[Param1] * 7 + [Param2]")]
        public void DynamicContextAccess(string formula)
        {
            var expression = new Expression(formula);
            var lambda = expression.ToLambda<Context, int>();

            var context = new Context { Param1 = 4, Param2 = 9 };
            expression.EvaluateParameter += (name, args) =>
            {
                if (name == "Param1") args.Result = context.Param1;
                if (name == "Param2") args.Result = context.Param2;
            };

            var m1 = Measure(i => expression.Evaluate());
            var m2 = Measure(i => lambda(context));

            PrintResult(formula, m1, m2);
        }

        [Theory]
        [InlineData("Foo([Param1] * 7, [Param2])")]
        public void FunctionWithDynamicParameterAccess(string formula)
        {
            var expression = new Expression(formula);
            var lambda = expression.ToLambda<Context, int>();

            var context = new Context { Param1 = 4, Param2 = 9 };
            expression.EvaluateParameter += (name, args) =>
            {
                if (name == "Param1") args.Result = context.Param1;
                if (name == "Param2") args.Result = context.Param2;
            };
            expression.EvaluateFunction += (name, args) =>
            {
                if (name == "Foo")
                {
                    var param = args.EvaluateParameters();
                    args.Result = context.Foo((int) param[0], (int) param[1]);
                }
            };

            var m1 = Measure(i => expression.Evaluate());
            var m2 = Measure(i => lambda(context));

            PrintResult(formula, m1, m2);
        }

        private TimeSpan Measure(Action<int> action)
        {
            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < Iterations; i++)
                action(i);
            sw.Stop();
            return sw.Elapsed;
        }

        private void PrintResult(string formula, TimeSpan m1, TimeSpan m2)
        {
            _output.WriteLine(new string('-', 60));
            _output.WriteLine("Formula: {0}", formula);
            _output.WriteLine("Expression: {0:N} evaluations / sec", Iterations / m1.TotalSeconds);
            _output.WriteLine("Lambda: {0:N} evaluations / sec", Iterations / m2.TotalSeconds);
            var speedup = (Iterations / m2.TotalSeconds) / (Iterations / m1.TotalSeconds) - 1;
            _output.WriteLine("Lambda Speedup: {0:P}%", speedup);
            _output.WriteLine(new string('-', 60));

            speedup.Should().BeInRange(2, 2000, "we should get reasonable speedup using lambda expression");
        }
    }
}
