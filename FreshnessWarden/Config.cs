namespace FreshnessWarden;

public record Config(string Host, int Port, string Database, string User, string Password)
{
    public static Config Load()
    {
        var host = GetRequired("GS_DB_HOST");
        var port = int.TryParse(Environment.GetEnvironmentVariable("GS_DB_PORT"), out var parsed)
            ? parsed
            : 5432;
        var database = GetRequired("GS_DB_NAME");
        var user = GetRequired("GS_DB_USER");
        var password = GetRequired("GS_DB_PASSWORD");

        return new Config(host, port, database, user, password);
    }

    private static string GetRequired(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required environment variable {key}.");
        }

        return value.Trim();
    }

    public string ConnectionString =>
        $"Host={Host};Port={Port};Database={Database};Username={User};Password={Password};SSL Mode=Require;Trust Server Certificate=true";
}
