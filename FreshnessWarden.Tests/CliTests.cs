using FreshnessWarden;

namespace FreshnessWarden.Tests;

public class CliTests
{
    [Fact]
    public void ParsesOptionsWithValues()
    {
        var options = Cli.ParseOptions(new[] { "--name", "Source A", "--sla-hours", "24" });

        Assert.Equal("Source A", options["name"]);
        Assert.Equal("24", options["sla-hours"]);
    }

    [Fact]
    public void ParsesFlagsWithoutValuesAsEmptyString()
    {
        var options = Cli.ParseOptions(new[] { "--dry-run" });

        Assert.True(options.ContainsKey("dry-run"));
        Assert.Equal("", options["dry-run"]);
    }

    [Fact]
    public void RequireStatusAllowsKnownValues()
    {
        var options = new Dictionary<string, string>
        {
            ["status"] = "warning"
        };

        var status = Cli.RequireStatus(options, "status");

        Assert.Equal("warning", status);
    }

    [Fact]
    public void RequireStatusRejectsUnknownValues()
    {
        var options = new Dictionary<string, string>
        {
            ["status"] = "bad"
        };

        Assert.Throws<InvalidOperationException>(() => Cli.RequireStatus(options, "status"));
    }

    [Fact]
    public void EnsureAnyUpdateProvidedThrowsWhenEmpty()
    {
        Assert.Throws<InvalidOperationException>(() => Cli.EnsureAnyUpdateProvided(null, null, false));
    }

    [Fact]
    public void EnsureAnyUpdateProvidedAcceptsAnyField()
    {
        Cli.EnsureAnyUpdateProvided("Owner", null, false);
    }

    [Fact]
    public void PrintOwnerSummaryHandlesEmptyList()
    {
        using var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            Cli.PrintOwnerSummary(Array.Empty<OwnerSummary>(), 7);
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("No owners registered.", writer.ToString());
    }

    [Fact]
    public void PrintOwnerSummaryRendersOwnerMetrics()
    {
        var summaries = new List<OwnerSummary>
        {
            new OwnerSummary(
                "Scholar Ops",
                2,
                1,
                3,
                1,
                0,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        };

        using var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            Cli.PrintOwnerSummary(summaries, 7);
        }
        finally
        {
            Console.SetOut(original);
        }

        var output = writer.ToString();
        Assert.Contains("Owner summary for last 7 days:", output);
        Assert.Contains("- Scholar Ops (sources: 2, stale: 1)", output);
        Assert.Contains("checks: ok 3, warning 1, failed 0", output);
    }

    [Fact]
    public void PrintOwnerHealthRendersMetrics()
    {
        var health = new List<OwnerHealth>
        {
            new OwnerHealth(
                "Data Ops",
                3,
                1,
                4,
                1,
                0,
                2,
                new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                "warning")
        };

        using var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            Cli.PrintOwnerHealth(health, 14);
        }
        finally
        {
            Console.SetOut(original);
        }

        var output = writer.ToString();
        Assert.Contains("Owner health for last 14 days:", output);
        Assert.Contains("- Data Ops (sources: 3, stale: 1)", output);
        Assert.Contains("breaches: 2", output);
    }
}
