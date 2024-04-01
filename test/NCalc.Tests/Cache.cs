using Xunit;

namespace NCalc.Tests;
public class Cache
{
    [Fact]
    public void ShouldCacheWhenEnabled()
    {
        var startingCachedCompilations = Expression.CachedCompilations;

        var expression = "123.33 + 33.123".CreateExpression(EvaluateOptions.None);
        expression.Evaluate();

        Assert.True(Expression.CachedCompilations > startingCachedCompilations);
    }

    [Fact]
    public void ShouldNotCacheWhenNoCache()
    {
        var startingCachedCompilations = Expression.CachedCompilations;

        var expression = "123.44 + 33.124".CreateExpression(EvaluateOptions.NoCache);
        expression.Evaluate();

        Assert.Equal(startingCachedCompilations, Expression.CachedCompilations);
    }

    [Fact]
    public void ShouldCleanCache()
    {
        for (int i = 0; i < 1000; i++)
        {
            var expression = $"123.44 + {i}".CreateExpression();
            expression.Evaluate();
        }
    }
}
