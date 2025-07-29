namespace FreshnessWarden;

public static class HealthCalculator
{
    public static int CountBreaches(IReadOnlyList<DateTime> orderedChecksUtc, int slaHours)
    {
        if (orderedChecksUtc.Count < 2)
        {
            return 0;
        }

        var breaches = 0;
        for (var i = 1; i < orderedChecksUtc.Count; i++)
        {
            var gapHours = (orderedChecksUtc[i] - orderedChecksUtc[i - 1]).TotalHours;
            if (gapHours > slaHours)
            {
                breaches++;
            }
        }

        return breaches;
    }
}
