using Xunit;

namespace NCalc.Tests;

public class Cache
{
    [Fact]
    public void ShouldCacheWhenEnabled()
    {
        var startingCachedCompilations = Expression.TotalCachedCompilations;

        var expression = "123.33 + 33.123".CreateExpression(EvaluateOptions.None);
        expression.Evaluate();

        Assert.True(Expression.TotalCachedCompilations > startingCachedCompilations);
    }

    [Fact]
    public void ShouldNotCacheWhenNoCache()
    {
        var startingCachedCompilations = Expression.TotalCachedCompilations;

        var expression = "123.44 + 33.124".CreateExpression(EvaluateOptions.NoCache);
        expression.Evaluate();

        Assert.Equal(startingCachedCompilations, Expression.TotalCachedCompilations);
    }

    [Fact]
    public void ShouldCleanCache()
    {
        var cacheCleanInterval = Expression.CacheCleanInterval;
        for (int i = 0; i < cacheCleanInterval; i++)
        {
            $"123.44 + {i}"
                .CreateExpression()
                .Evaluate();
        }
    }
}
