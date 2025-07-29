using FreshnessWarden;

namespace FreshnessWarden.Tests;

public class StalenessTests
{
    [Fact]
    public void MarksMissingCheckAsStale()
    {
        var now = DateTime.UtcNow;
        Assert.True(Staleness.IsStale(null, 24, now));
    }

    [Fact]
    public void MarksRecentCheckAsFresh()
    {
        var now = DateTime.UtcNow;
        var last = now.AddHours(-4);
        Assert.False(Staleness.IsStale(last, 24, now));
    }

    [Fact]
    public void MarksOldCheckAsStale()
    {
        var now = DateTime.UtcNow;
        var last = now.AddHours(-30);
        Assert.True(Staleness.IsStale(last, 24, now));
    }
}
