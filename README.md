# Group Scholar Data Freshness Warden

Data freshness tracking for Group Scholar operational sources. This CLI logs source checks, flags stale feeds, and summarizes recent health signals.

## Features
- Track data sources with owners and SLAs
- Log freshness checks with status and details
- View latest status per source
- Review per-source health with SLA breach counts
- Remove sources no longer tracked
- List stale sources based on SLA hours
- Generate summary reports for the last N days

## Tech
- C# (.NET)
- PostgreSQL (via Npgsql)

## Setup

Set environment variables (never commit credentials):

```
export GS_DB_HOST=your-host
export GS_DB_PORT=23947
export GS_DB_NAME=postgres
export GS_DB_USER=ralph
export GS_DB_PASSWORD=your-password
```

Initialize schema + seed data:

```
dotnet run --project FreshnessWarden -- init-db
```

## Usage

```
dotnet run --project FreshnessWarden -- add-source --name "Scholar Application Export" --owner "Scholar Ops" --sla-hours 24 --notes "Daily export"

dotnet run --project FreshnessWarden -- log-check --source "Scholar Application Export" --status ok --details "Arrived on time"

dotnet run --project FreshnessWarden -- status

dotnet run --project FreshnessWarden -- status --owner "Scholar Ops"

dotnet run --project FreshnessWarden -- list-stale

dotnet run --project FreshnessWarden -- rollup

dotnet run --project FreshnessWarden -- source-history --name "Scholar Application Export" --limit 10

dotnet run --project FreshnessWarden -- source-health --days 14

dotnet run --project FreshnessWarden -- remove-source --name "Scholar Application Export"

dotnet run --project FreshnessWarden -- summary --days 7
```

## Testing

```
dotnet test
```
