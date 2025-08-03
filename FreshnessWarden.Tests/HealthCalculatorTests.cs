using FreshnessWarden;

namespace FreshnessWarden.Tests;

public class HealthCalculatorTests
{
    [Fact]
    public void ReturnsZeroWhenInsufficientChecks()
    {
        var now = DateTime.UtcNow;
        var checks = new List<DateTime> { now };

        var breaches = HealthCalculator.CountBreaches(checks, 12);

        Assert.Equal(0, breaches);
    }

    [Fact]
    public void CountsGapBreachesOverSla()
    {
        var start = DateTime.UtcNow.AddHours(-30);
        var checks = new List<DateTime>
        {
            start,
            start.AddHours(6),
            start.AddHours(20),
            start.AddHours(29)
        };

        var breaches = HealthCalculator.CountBreaches(checks, 12);

        Assert.Equal(1, breaches);
    }

    [Fact]
    public void IgnoresGapEqualToSla()
    {
        var start = DateTime.UtcNow.AddHours(-24);
        var checks = new List<DateTime>
        {
            start,
            start.AddHours(8),
            start.AddHours(16)
        };

        var breaches = HealthCalculator.CountBreaches(checks, 8);

        Assert.Equal(0, breaches);
    }
}
