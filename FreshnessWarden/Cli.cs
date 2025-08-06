using System.Globalization;

namespace FreshnessWarden;

public static class Cli
{
    public static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var current = list[i];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = current[2..];
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (i + 1 >= list.Count || list[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[key] = "";
                continue;
            }

            options[key] = list[i + 1];
            i++;
        }

        return options;
    }

    public static string Require(Dictionary<string, string> options, string key)
    {
        if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required option --{key}.");
        }

        return value.Trim();
    }

    public static int RequireInt(Dictionary<string, string> options, string key)
    {
        var value = Require(options, key);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"Option --{key} must be an integer.");
        }

        return parsed;
    }

    public static int? OptionalInt(Dictionary<string, string> options, string key)
    {
        if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"Option --{key} must be an integer.");
        }

        return parsed;
    }

    public static string? Optional(Dictionary<string, string> options, string key)
    {
        return options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    public static string RequireStatus(Dictionary<string, string> options, string key)
    {
        var status = Require(options, key).ToLowerInvariant();
        return status switch
        {
            "ok" or "warning" or "failed" => status,
            _ => throw new InvalidOperationException("Status must be one of: ok, warning, failed.")
        };
    }

    public static void EnsureAnyUpdateProvided(string? owner, int? slaHours, bool notesSpecified)
    {
        if (owner == null && slaHours == null && !notesSpecified)
        {
            throw new InvalidOperationException("Provide at least one of --owner, --sla-hours, --notes, or --clear-notes.");
        }
    }

    public static void PrintStale(IReadOnlyList<StaleSource> staleSources)
    {
        if (staleSources.Count == 0)
        {
            Console.WriteLine("No stale sources detected.");
            return;
        }

        Console.WriteLine("Stale sources:");
        foreach (var source in staleSources)
        {
            var last = source.LastCheckedAt.HasValue
                ? source.LastCheckedAt.Value.ToString("u")
                : "never";
            Console.WriteLine($"- {source.Name} (owner: {source.Owner}, SLA: {source.SlaHours}h, last: {last})");
        }
    }

    public static void PrintSummary(SummaryReport report)
    {
        Console.WriteLine($"Summary for last {report.Days} days:");
        Console.WriteLine($"- ok: {report.OkCount}");
        Console.WriteLine($"- warning: {report.WarningCount}");
        Console.WriteLine($"- failed: {report.FailedCount}");
        Console.WriteLine($"- stale sources: {report.StaleSources.Count}");
        if (report.StaleSources.Count > 0)
        {
            foreach (var source in report.StaleSources)
            {
                var last = source.LastCheckedAt.HasValue
                    ? source.LastCheckedAt.Value.ToString("u")
                    : "never";
                Console.WriteLine($"  - {source.Name} (owner: {source.Owner}, last: {last})");
            }
        }
    }

    public static void PrintRollup(IReadOnlyList<SourceRollup> sources)
    {
        if (sources.Count == 0)
        {
            Console.WriteLine("No sources registered.");
            return;
        }

        Console.WriteLine("Source rollup:");
        foreach (var source in sources)
        {
            var last = source.LastCheckedAt.HasValue
                ? source.LastCheckedAt.Value.ToString("u")
                : "never";
            var nextDue = source.NextDueAt.HasValue
                ? source.NextDueAt.Value.ToString("u")
                : "n/a";
            var status = string.IsNullOrWhiteSpace(source.LastStatus) ? "n/a" : source.LastStatus;
            var staleLabel = source.IsStale ? "stale" : "fresh";
            Console.WriteLine($"- {source.Name} ({source.Owner}, SLA: {source.SlaHours}h)");
            Console.WriteLine($"  last: {last} | status: {status} | next due: {nextDue} | {staleLabel}");
            if (!string.IsNullOrWhiteSpace(source.LastDetails))
            {
                Console.WriteLine($"  details: {source.LastDetails}");
            }
        }
    }

    public static void PrintHistory(string sourceName, IReadOnlyList<SourceCheck> history)
    {
        Console.WriteLine($"Recent checks for {sourceName}:");
        if (history.Count == 0)
        {
            Console.WriteLine("  (no checks yet)");
            return;
        }

        foreach (var entry in history)
        {
            var stamp = entry.CheckedAt.ToString("u");
            var details = string.IsNullOrWhiteSpace(entry.Details) ? "" : $" | {entry.Details}";
            Console.WriteLine($"- {stamp} | {entry.Status}{details}");
        }
    }

    public static void PrintStatus(IReadOnlyList<SourceStatus> statuses)
    {
        if (statuses.Count == 0)
        {
            Console.WriteLine("No sources found.");
            return;
        }

        Console.WriteLine("Source status:");
        foreach (var status in statuses)
        {
            var last = status.LastCheckedAt.HasValue
                ? status.LastCheckedAt.Value.ToString("u")
                : "never";
            var lastStatus = string.IsNullOrWhiteSpace(status.LastStatus) ? "none" : status.LastStatus;
            Console.WriteLine($"- {status.Name} (owner: {status.Owner}, SLA: {status.SlaHours}h, last: {last}, status: {lastStatus})");
        }
    }

    public static void PrintSourceHealth(IReadOnlyList<SourceHealth> health, int days)
    {
        if (health.Count == 0)
        {
            Console.WriteLine("No sources registered.");
            return;
        }

        Console.WriteLine($"Source health for last {days} days:");
        foreach (var source in health)
        {
            var last = source.LastCheckedAt.HasValue
                ? source.LastCheckedAt.Value.ToString("u")
                : "never";
            var hoursSince = source.HoursSinceLast.HasValue
                ? $"{source.HoursSinceLast.Value}h"
                : "n/a";
            var lastStatus = string.IsNullOrWhiteSpace(source.LastStatus) ? "none" : source.LastStatus;
            var staleLabel = source.IsStale ? "stale" : "fresh";
            Console.WriteLine($"- {source.Name} ({source.Owner}, SLA: {source.SlaHours}h, {staleLabel})");
            Console.WriteLine($"  last: {last} | status: {lastStatus} | age: {hoursSince}");
            Console.WriteLine($"  checks: {source.TotalChecks} (ok {source.OkCount}, warning {source.WarningCount}, failed {source.FailedCount}) | breaches: {source.BreachCount}");
        }
    }

    public static void PrintOwnerSummary(IReadOnlyList<OwnerSummary> summaries, int days)
    {
        if (summaries.Count == 0)
        {
            Console.WriteLine("No owners registered.");
            return;
        }

        Console.WriteLine($"Owner summary for last {days} days:");
        foreach (var summary in summaries)
        {
            var latest = summary.LatestCheckAt.HasValue
                ? summary.LatestCheckAt.Value.ToString("u")
                : "never";
            Console.WriteLine($"- {summary.Owner} (sources: {summary.TotalSources}, stale: {summary.StaleSources})");
            Console.WriteLine($"  checks: ok {summary.OkCount}, warning {summary.WarningCount}, failed {summary.FailedCount} | latest: {latest}");
        }
    }

    public static void PrintOwnerHealth(IReadOnlyList<OwnerHealth> health, int days)
    {
        if (health.Count == 0)
        {
            Console.WriteLine("No owners registered.");
            return;
        }

        Console.WriteLine($"Owner health for last {days} days:");
        foreach (var owner in health)
        {
            var last = owner.LastCheckedAt.HasValue
                ? owner.LastCheckedAt.Value.ToString("u")
                : "never";
            var lastStatus = string.IsNullOrWhiteSpace(owner.LastStatus) ? "none" : owner.LastStatus;
            Console.WriteLine($"- {owner.Owner} (sources: {owner.SourceCount}, stale: {owner.StaleCount})");
            Console.WriteLine($"  last: {last} | status: {lastStatus} | breaches: {owner.BreachCount}");
            Console.WriteLine($"  checks: {owner.OkCount + owner.WarningCount + owner.FailedCount} (ok {owner.OkCount}, warning {owner.WarningCount}, failed {owner.FailedCount})");
        }
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Groupscholar Data Freshness Warden");
        Console.WriteLine("Commands:");
        Console.WriteLine("  init-db");
        Console.WriteLine("  add-source --name <name> --owner <owner> --sla-hours <hours> [--notes <notes>]");
        Console.WriteLine("  log-check --source <name> --status <ok|warning|failed> [--details <details>]");
        Console.WriteLine("  status [--owner <owner>]");
        Console.WriteLine("  rollup");
        Console.WriteLine("  list-stale");
        Console.WriteLine("  source-history --name <name> [--limit <limit>]");
        Console.WriteLine("  source-health [--days <days>]");
        Console.WriteLine("  owner-summary [--days <days>]");
        Console.WriteLine("  owner-health [--days <days>]");
        Console.WriteLine("  update-source --name <name> [--owner <owner>] [--sla-hours <hours>] [--notes <notes> | --clear-notes]");
        Console.WriteLine("  remove-source --name <name>");
        Console.WriteLine("  summary [--days <days>]");
        Console.WriteLine();
        Console.WriteLine("Environment variables:");
        Console.WriteLine("  GS_DB_HOST, GS_DB_PORT, GS_DB_NAME, GS_DB_USER, GS_DB_PASSWORD");
    }
}
