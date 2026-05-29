using Npgsql;

namespace TravelBot.Data;

public static class DatabaseConnection
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var password = configuration["Supabase:Password"]
            ?? Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Укажите пароль Supabase в appsettings.json " +
                "или переменной SUPABASE_DB_PASSWORD / Supabase__Password.");
        }

        var usePooler = configuration.GetValue("Supabase:UsePooler", false);
        var projectRef = configuration["Supabase:ProjectRef"] ?? "ztpllfixhmifirwadcrs";
        var database = configuration["Supabase:Database"] ?? "postgres";

        if (usePooler)
        {
            var poolerHost = configuration["Supabase:PoolerHost"]
                ?? Environment.GetEnvironmentVariable("SUPABASE_POOLER_HOST");

            if (string.IsNullOrWhiteSpace(poolerHost))
            {
                throw new InvalidOperationException(
                    "Шаг 4 (Render): укажите Supabase:PoolerHost или SUPABASE_POOLER_HOST.");
            }

            return Build(
                host: poolerHost,
                port: configuration["Supabase:PoolerPort"] ?? "6543",
                database: database,
                username: configuration["Supabase:PoolerUsername"] ?? $"postgres.{projectRef}",
                password: password);
        }

        var host = configuration["Supabase:Host"] ?? $"db.{projectRef}.supabase.co";

        return Build(
            host: host,
            port: configuration["Supabase:Port"] ?? "5432",
            database: database,
            username: configuration["Supabase:Username"] ?? "postgres",
            password: password);
    }

    public static string GetHostForLogging(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return string.IsNullOrWhiteSpace(builder.Host) ? "unknown" : builder.Host;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string Build(string host, string port, string database, string username, string password) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.Parse(port),
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require
        }.ConnectionString;
}
