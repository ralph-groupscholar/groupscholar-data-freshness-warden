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
}
