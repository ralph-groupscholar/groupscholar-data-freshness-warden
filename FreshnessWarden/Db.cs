using Npgsql;

namespace FreshnessWarden;

public sealed class Db : IDisposable
{
    private const string Schema = "gs_data_freshness_warden";
    private readonly NpgsqlConnection _connection;

    public Db(Config config)
    {
        _connection = new NpgsqlConnection(config.ConnectionString);
        _connection.Open();
    }

    public void InitializeSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            create schema if not exists {Schema};
            create table if not exists {Schema}.sources (
                id serial primary key,
                name text not null unique,
                owner text not null,
                sla_hours integer not null,
                notes text,
                created_at timestamptz not null default now()
            );
            create table if not exists {Schema}.checks (
                id serial primary key,
                source_id integer not null references {Schema}.sources(id) on delete cascade,
                status text not null,
                checked_at timestamptz not null default now(),
                details text
            );
            create index if not exists checks_source_id_checked_at_idx
                on {Schema}.checks (source_id, checked_at desc);
        ";
        cmd.ExecuteNonQuery();
    }

    public void SeedIfEmpty()
    {
        using var countCmd = _connection.CreateCommand();
        countCmd.CommandText = $"select count(*) from {Schema}.sources";
        var count = (long)countCmd.ExecuteScalar()!;
        if (count > 0)
        {
            return;
        }

        AddSource("Scholar Application Export", "Scholar Ops", 24, "Daily export from CRM.");
        AddSource("Mentor Availability Sheet", "Mentor Lead", 72, "Updated manually by mentor team.");
        AddSource("Award Disbursement Feed", "Finance", 12, "Bank feed refresh cadence.");

        LogCheck("Scholar Application Export", "ok", "Export landed on time.");
        LogCheck("Mentor Availability Sheet", "warning", "Missing two mentors this week.");
        LogCheck("Award Disbursement Feed", "failed", "No feed received in 24 hours.");
    }

    public void AddSource(string name, string owner, int slaHours, string? notes)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            insert into {Schema}.sources (name, owner, sla_hours, notes)
            values (@name, @owner, @sla, @notes)
            on conflict (name) do nothing;
        ";
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("owner", owner);
        cmd.Parameters.AddWithValue("sla", slaHours);
        cmd.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public bool UpdateSource(string name, string? owner, int? slaHours, string? notes, bool notesSpecified)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            update {Schema}.sources
            set owner = coalesce(@owner, owner),
                sla_hours = coalesce(@sla_hours, sla_hours),
                notes = case when @notes_specified then @notes else notes end
            where name = @name;
        ";
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("owner", (object?)owner ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sla_hours", (object?)slaHours ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes_specified", notesSpecified);
        cmd.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);
        return cmd.ExecuteNonQuery() > 0;
    }

    public void LogCheck(string sourceName, string status, string? details)
    {
        var sourceId = GetSourceId(sourceName);
        if (sourceId == null)
        {
            throw new InvalidOperationException($"Source '{sourceName}' not found.");
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            insert into {Schema}.checks (source_id, status, details)
            values (@sourceId, @status, @details);
        ";
        cmd.Parameters.AddWithValue("sourceId", sourceId.Value);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("details", (object?)details ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<StaleSource> GetStaleSources()
    {
        var sources = GetSourcesWithLastCheck();
        var nowUtc = DateTime.UtcNow;
        return sources
            .Where(source => Staleness.IsStale(source.LastCheckedAt, source.SlaHours, nowUtc))
            .ToList();
    }

    public SummaryReport GetSummary(int days)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var summaries = new List<CheckSummary>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $@"
                select status, count(*)
                from {Schema}.checks
                where checked_at >= @since
                group by status;
            ";
            cmd.Parameters.AddWithValue("since", since);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                summaries.Add(new CheckSummary(reader.GetString(0), reader.GetInt32(1)));
            }
        }

        var ok = summaries.FirstOrDefault(s => s.Status == "ok")?.Count ?? 0;
        var warning = summaries.FirstOrDefault(s => s.Status == "warning")?.Count ?? 0;
        var failed = summaries.FirstOrDefault(s => s.Status == "failed")?.Count ?? 0;
        var stale = GetStaleSources();

        return new SummaryReport(days, ok, warning, failed, stale);
    }

    public IReadOnlyList<OwnerSummary> GetOwnerSummary(int days)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var nowUtc = DateTime.UtcNow;

        var sourceStats = new List<(string Owner, int SlaHours, DateTime? LastCheckedAt)>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $@"
                select s.owner, s.sla_hours, last_check.checked_at
                from {Schema}.sources s
                left join lateral (
                    select c.checked_at
                    from {Schema}.checks c
                    where c.source_id = s.id
                    order by c.checked_at desc, c.id desc
                    limit 1
                ) as last_check on true
                order by s.owner;
            ";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var lastChecked = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2).ToUniversalTime();
                sourceStats.Add((reader.GetString(0), reader.GetInt32(1), lastChecked));
            }
        }

        var ownerCounts = new Dictionary<string, OwnerSummaryBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sourceStats)
        {
            if (!ownerCounts.TryGetValue(source.Owner, out var builder))
            {
                builder = new OwnerSummaryBuilder(source.Owner);
                ownerCounts[source.Owner] = builder;
            }

            builder.TotalSources++;
            if (Staleness.IsStale(source.LastCheckedAt, source.SlaHours, nowUtc))
            {
                builder.StaleSources++;
            }

            if (source.LastCheckedAt.HasValue)
            {
                builder.LatestCheckAt = builder.LatestCheckAt.HasValue
                    ? (builder.LatestCheckAt > source.LastCheckedAt ? builder.LatestCheckAt : source.LastCheckedAt)
                    : source.LastCheckedAt;
            }
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $@"
                select s.owner, c.status, count(*)
                from {Schema}.checks c
                inner join {Schema}.sources s on s.id = c.source_id
                where c.checked_at >= @since
                group by s.owner, c.status
                order by s.owner;
            ";
            cmd.Parameters.AddWithValue("since", since);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var owner = reader.GetString(0);
                if (!ownerCounts.TryGetValue(owner, out var builder))
                {
                    builder = new OwnerSummaryBuilder(owner);
                    ownerCounts[owner] = builder;
                }

                var status = reader.GetString(1);
                var count = reader.GetInt32(2);
                switch (status)
                {
                    case "ok":
                        builder.OkCount = count;
                        break;
                    case "warning":
                        builder.WarningCount = count;
                        break;
                    case "failed":
                        builder.FailedCount = count;
                        break;
                }
            }
        }

        return ownerCounts.Values
            .Select(builder => builder.Build())
            .OrderBy(summary => summary.Owner)
            .ToList();
    }

    private sealed class OwnerSummaryBuilder
    {
        public OwnerSummaryBuilder(string owner)
        {
            Owner = owner;
        }

        public string Owner { get; }
        public int TotalSources { get; set; }
        public int StaleSources { get; set; }
        public int OkCount { get; set; }
        public int WarningCount { get; set; }
        public int FailedCount { get; set; }
        public DateTime? LatestCheckAt { get; set; }

        public OwnerSummary Build()
        {
            return new OwnerSummary(
                Owner,
                TotalSources,
                StaleSources,
                OkCount,
                WarningCount,
                FailedCount,
                LatestCheckAt);
        }
    }

    public IReadOnlyList<SourceHealth> GetSourceHealth(int days)
    {
        var sources = GetSources();
        var since = DateTime.UtcNow.AddDays(-days);
        var nowUtc = DateTime.UtcNow;

        var checksBySource = new Dictionary<int, List<SourceCheckWindow>>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $@"
                select source_id, status, checked_at, is_recent
                from (
                    select c.source_id, c.status, c.checked_at, true as is_recent
                    from {Schema}.checks c
                    where c.checked_at >= @since
                    union all
                    select distinct on (c.source_id) c.source_id, c.status, c.checked_at, false as is_recent
                    from {Schema}.checks c
                    where c.checked_at < @since
                    order by c.source_id, c.checked_at desc
                ) t
                order by source_id, checked_at;
            ";
            cmd.Parameters.AddWithValue("since", since);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var sourceId = reader.GetInt32(0);
                if (!checksBySource.TryGetValue(sourceId, out var list))
                {
                    list = new List<SourceCheckWindow>();
                    checksBySource[sourceId] = list;
                }

                var checkedAt = reader.GetDateTime(2).ToUniversalTime();
                list.Add(new SourceCheckWindow(
                    checkedAt,
                    reader.GetString(1),
                    reader.GetBoolean(3)));
            }
        }

        var results = new List<SourceHealth>();
        foreach (var source in sources)
        {
            checksBySource.TryGetValue(source.Id, out var checks);
            checks ??= new List<SourceCheckWindow>();

            var orderedChecks = checks
                .OrderBy(check => check.CheckedAt)
                .ToList();

            var lastCheck = orderedChecks.LastOrDefault();
            var lastCheckedAt = lastCheck?.CheckedAt;
            var hoursSinceLast = lastCheckedAt.HasValue
                ? (int)Math.Floor((nowUtc - lastCheckedAt.Value).TotalHours)
                : null;

            var recentChecks = checks.Where(check => check.IsRecent).ToList();
            var ok = recentChecks.Count(check => check.Status == "ok");
            var warning = recentChecks.Count(check => check.Status == "warning");
            var failed = recentChecks.Count(check => check.Status == "failed");
            var breachCount = HealthCalculator.CountBreaches(
                orderedChecks.Select(check => check.CheckedAt).ToList(),
                source.SlaHours);

            results.Add(new SourceHealth(
                source.Id,
                source.Name,
                source.Owner,
                source.SlaHours,
                lastCheckedAt,
                hoursSinceLast,
                lastCheck?.Status,
                Staleness.IsStale(lastCheckedAt, source.SlaHours, nowUtc),
                ok,
                warning,
                failed,
                breachCount,
                recentChecks.Count));
        }

        return results
            .OrderBy(result => result.Name)
            .ToList();
    }

    public IReadOnlyList<OwnerHealth> GetOwnerHealth(int days)
    {
        return OwnerHealthCalculator.Build(GetSourceHealth(days));
    }

    public IReadOnlyList<SourceRollup> GetSourceRollups()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            select s.id, s.name, s.owner, s.sla_hours,
                   c.status, c.checked_at, c.details
            from {Schema}.sources s
            left join lateral (
                select status, checked_at, details
                from {Schema}.checks
                where source_id = s.id
                order by checked_at desc
                limit 1
            ) c on true
            order by s.name;
        ";

        var results = new List<SourceRollup>();
        var nowUtc = DateTime.UtcNow;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var lastStatus = reader.IsDBNull(4) ? null : reader.GetString(4);
            var lastChecked = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5).ToUniversalTime();
            var lastDetails = reader.IsDBNull(6) ? null : reader.GetString(6);
            var slaHours = reader.GetInt32(3);
            var isStale = Staleness.IsStale(lastChecked, slaHours, nowUtc);
            var nextDue = lastChecked.HasValue ? lastChecked.Value.AddHours(slaHours) : (DateTime?)null;

            results.Add(new SourceRollup(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                slaHours,
                lastStatus,
                lastChecked,
                lastDetails,
                isStale,
                nextDue));
        }

        return results;
    }

    public IReadOnlyList<SourceCheck> GetSourceHistory(string sourceName, int limit)
    {
        var sourceId = GetSourceId(sourceName);
        if (sourceId == null)
        {
            throw new InvalidOperationException($"Source '{sourceName}' not found.");
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            select checked_at, status, details
            from {Schema}.checks
            where source_id = @sourceId
            order by checked_at desc
            limit @limit;
        ";
        cmd.Parameters.AddWithValue("sourceId", sourceId.Value);
        cmd.Parameters.AddWithValue("limit", limit);

        var results = new List<SourceCheck>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SourceCheck(
                reader.GetDateTime(0).ToUniversalTime(),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return results;
    }

    public IReadOnlyList<SourceStatus> GetSourceStatus()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            select s.id,
                   s.name,
                   s.owner,
                   s.sla_hours,
                   last_check.checked_at,
                   last_check.status
            from {Schema}.sources s
            left join lateral (
                select c.checked_at, c.status
                from {Schema}.checks c
                where c.source_id = s.id
                order by c.checked_at desc, c.id desc
                limit 1
            ) as last_check on true
            order by s.name;
        ";

        var results = new List<SourceStatus>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var lastChecked = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
            var lastStatus = reader.IsDBNull(5) ? null : reader.GetString(5);
            results.Add(new SourceStatus(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                lastChecked?.ToUniversalTime(),
                lastStatus));
        }

        return results;
    }

    public IReadOnlyList<SourceStatus> GetSourceStatusForOwner(string owner)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            select s.id,
                   s.name,
                   s.owner,
                   s.sla_hours,
                   last_check.checked_at,
                   last_check.status
            from {Schema}.sources s
            left join lateral (
                select c.checked_at, c.status
                from {Schema}.checks c
                where c.source_id = s.id
                order by c.checked_at desc, c.id desc
                limit 1
            ) as last_check on true
            where lower(s.owner) = lower(@owner)
            order by s.name;
        ";
        cmd.Parameters.AddWithValue("owner", owner);

        var results = new List<SourceStatus>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var lastChecked = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
            var lastStatus = reader.IsDBNull(5) ? null : reader.GetString(5);
            results.Add(new SourceStatus(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                lastChecked?.ToUniversalTime(),
                lastStatus));
        }

        return results;
    }

    public bool RemoveSource(string name)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            delete from {Schema}.sources
            where name = @name;
        ";
        cmd.Parameters.AddWithValue("name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    private List<SourceInfo> GetSources()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            select id, name, owner, sla_hours
            from {Schema}.sources
            order by name;
        ";

        var results = new List<SourceInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SourceInfo(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)));
        }

        return results;
    }

    private List<StaleSource> GetSourcesWithLastCheck()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            select s.id, s.name, s.owner, s.sla_hours,
                   max(c.checked_at) as last_checked_at
            from {Schema}.sources s
            left join {Schema}.checks c on c.source_id = s.id
            group by s.id, s.name, s.owner, s.sla_hours
            order by s.name;
        ";

        var results = new List<StaleSource>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var lastChecked = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
            results.Add(new StaleSource(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                lastChecked?.ToUniversalTime()));
        }

        return results;
    }

    private int? GetSourceId(string name)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"select id from {Schema}.sources where name = @name";
        cmd.Parameters.AddWithValue("name", name);
        var result = cmd.ExecuteScalar();
        return result == null ? null : Convert.ToInt32(result);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
