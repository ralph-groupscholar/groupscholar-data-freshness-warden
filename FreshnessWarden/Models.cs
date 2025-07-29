namespace FreshnessWarden;

public record SourceInfo(int Id, string Name, string Owner, int SlaHours);

public record StaleSource(int Id, string Name, string Owner, int SlaHours, DateTime? LastCheckedAt);

public record SummaryReport(int Days, int OkCount, int WarningCount, int FailedCount, IReadOnlyList<StaleSource> StaleSources);

public record CheckSummary(string Status, int Count);

public record SourceCheckWindow(DateTime CheckedAt, string Status, bool IsRecent);

public record SourceHealth(
    int Id,
    string Name,
    string Owner,
    int SlaHours,
    DateTime? LastCheckedAt,
    int? HoursSinceLast,
    string? LastStatus,
    bool IsStale,
    int OkCount,
    int WarningCount,
    int FailedCount,
    int BreachCount,
    int TotalChecks);

public record SourceRollup(
    int Id,
    string Name,
    string Owner,
    int SlaHours,
    string? LastStatus,
    DateTime? LastCheckedAt,
    string? LastDetails,
    bool IsStale,
    DateTime? NextDueAt);

public record SourceCheck(DateTime CheckedAt, string Status, string? Details);

public record SourceStatus(int Id, string Name, string Owner, int SlaHours, DateTime? LastCheckedAt, string? LastStatus);
