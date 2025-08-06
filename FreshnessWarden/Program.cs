using FreshnessWarden;

var argsList = args.ToList();
if (argsList.Count == 0 || argsList[0] is "help" or "--help" or "-h")
{
    Cli.PrintHelp();
    return;
}

var command = argsList[0];
var options = Cli.ParseOptions(argsList.Skip(1));

try
{
    var config = Config.Load();
    using var db = new Db(config);

    switch (command)
    {
        case "init-db":
            db.InitializeSchema();
            db.SeedIfEmpty();
            Console.WriteLine("Database initialized and seeded.");
            break;
        case "add-source":
            db.AddSource(
                Cli.Require(options, "name"),
                Cli.Require(options, "owner"),
                Cli.RequireInt(options, "sla-hours"),
                Cli.Optional(options, "notes"));
            Console.WriteLine("Source added.");
            break;
        case "log-check":
            db.LogCheck(
                Cli.Require(options, "source"),
                Cli.RequireStatus(options, "status"),
                Cli.Optional(options, "details"));
            Console.WriteLine("Check logged.");
            break;
        case "status":
            var owner = Cli.Optional(options, "owner");
            var statuses = owner == null
                ? db.GetSourceStatus()
                : db.GetSourceStatusForOwner(owner);
            Cli.PrintStatus(statuses);
            break;
        case "rollup":
            Cli.PrintRollup(db.GetSourceRollups());
            break;
        case "list-stale":
            Cli.PrintStale(db.GetStaleSources());
            break;
        case "source-history":
            var sourceName = Cli.Require(options, "name");
            var limit = Cli.OptionalInt(options, "limit") ?? 10;
            Cli.PrintHistory(sourceName, db.GetSourceHistory(sourceName, limit));
            break;
        case "source-health":
            var healthDays = Cli.OptionalInt(options, "days") ?? 14;
            Cli.PrintSourceHealth(db.GetSourceHealth(healthDays), healthDays);
            break;
        case "owner-summary":
            var ownerDays = Cli.OptionalInt(options, "days") ?? 7;
            Cli.PrintOwnerSummary(db.GetOwnerSummary(ownerDays), ownerDays);
            break;
        case "owner-health":
            var ownerHealthDays = Cli.OptionalInt(options, "days") ?? 14;
            Cli.PrintOwnerHealth(db.GetOwnerHealth(ownerHealthDays), ownerHealthDays);
            break;
        case "update-source":
            var updateName = Cli.Require(options, "name");
            var updateOwner = Cli.Optional(options, "owner");
            var updateSla = Cli.OptionalInt(options, "sla-hours");
            var notesSpecified = options.ContainsKey("notes") || options.ContainsKey("clear-notes");
            var updateNotes = options.ContainsKey("clear-notes") ? null : Cli.Optional(options, "notes");
            Cli.EnsureAnyUpdateProvided(updateOwner, updateSla, notesSpecified);
            var updated = db.UpdateSource(updateName, updateOwner, updateSla, updateNotes, notesSpecified);
            Console.WriteLine(updated ? "Source updated." : "Source not found.");
            break;
        case "remove-source":
            var removed = db.RemoveSource(Cli.Require(options, "name"));
            Console.WriteLine(removed ? "Source removed." : "Source not found.");
            break;
        case "summary":
            var days = Cli.OptionalInt(options, "days") ?? 7;
            Cli.PrintSummary(db.GetSummary(days));
            break;
        default:
            Console.WriteLine($"Unknown command: {command}");
            Cli.PrintHelp();
            break;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}
