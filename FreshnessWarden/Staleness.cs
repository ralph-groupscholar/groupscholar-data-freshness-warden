namespace FreshnessWarden;

public static class Staleness
{
    public static bool IsStale(DateTime? lastCheckedAt, int slaHours, DateTime nowUtc)
    {
        if (!lastCheckedAt.HasValue)
        {
            return true;
        }

        var age = nowUtc - lastCheckedAt.Value;
        return age.TotalHours > slaHours;
    }
}
