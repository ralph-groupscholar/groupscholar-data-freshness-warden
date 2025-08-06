using FreshnessWarden;

namespace FreshnessWarden.Tests;

public class OwnerHealthCalculatorTests
{
    [Fact]
    public void AggregatesHealthByOwnerCaseInsensitive()
    {
        var now = DateTime.UtcNow;
        var sources = new List<SourceHealth>
        {
            new(
                1,
                "CRM Export",
                "Data Ops",
                24,
                now.AddHours(-2),
                2,
                "ok",
                false,
                3,
                1,
                0,
                0,
                4),
            new(
                2,
                "Mentor Sheet",
                "data ops",
                48,
                now.AddHours(-5),
                5,
                "warning",
                true,
                2,
                2,
                1,
                2,
                5),
            new(
                3,
                "Finance Feed",
                "Finance",
                12,
                now.AddHours(-1),
                1,
                "failed",
                false,
                0,
                1,
                2,
                1,
                3)
        };

        var results = OwnerHealthCalculator.Build(sources);

        var dataOps = results.Single(owner => owner.Owner == "Data Ops");
        Assert.Equal(2, dataOps.SourceCount);
        Assert.Equal(1, dataOps.StaleCount);
        Assert.Equal(5, dataOps.OkCount);
        Assert.Equal(3, dataOps.WarningCount);
        Assert.Equal(1, dataOps.FailedCount);
        Assert.Equal(2, dataOps.BreachCount);
        Assert.Equal(now.AddHours(-2), dataOps.LastCheckedAt);
        Assert.Equal("ok", dataOps.LastStatus);
    }

    [Fact]
    public void LeavesLastCheckNullWhenNoChecksExist()
    {
        var sources = new List<SourceHealth>
        {
            new(
                1,
                "Prospect Feed",
                "Pipeline",
                24,
                null,
                null,
                null,
                true,
                0,
                0,
                0,
                0,
                0)
        };

        var results = OwnerHealthCalculator.Build(sources);

        var pipeline = results.Single(owner => owner.Owner == "Pipeline");
        Assert.Null(pipeline.LastCheckedAt);
        Assert.Null(pipeline.LastStatus);
    }
}
