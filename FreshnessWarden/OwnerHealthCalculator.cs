namespace FreshnessWarden;

public static class OwnerHealthCalculator
{
    public static IReadOnlyList<OwnerHealth> Build(IEnumerable<SourceHealth> sources)
    {
        return sources
            .GroupBy(source => source.Owner, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var last = group
                    .Where(source => source.LastCheckedAt.HasValue)
                    .OrderByDescending(source => source.LastCheckedAt)
                    .FirstOrDefault();

                return new OwnerHealth(
                    group.First().Owner,
                    group.Count(),
                    group.Count(source => source.IsStale),
                    group.Sum(source => source.OkCount),
                    group.Sum(source => source.WarningCount),
                    group.Sum(source => source.FailedCount),
                    group.Sum(source => source.BreachCount),
                    last?.LastCheckedAt,
                    last?.LastStatus);
            })
            .OrderBy(owner => owner.Owner)
            .ToList();
    }
}
