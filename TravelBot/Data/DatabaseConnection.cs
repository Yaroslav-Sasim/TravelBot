using Npgsql;

namespace TravelBot.Data;

public static class DatabaseConnection
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var explicitConnection = configuration["Supabase:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(explicitConnection))
            return NormalizeConnectionString(explicitConnection);

        var password = configuration["Supabase:Password"]
            ?? Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Укажите пароль Supabase в appsettings.json " +
                "или переменной SUPABASE_DB_PASSWORD / Supabase__Password.");
        }

        var projectRef = configuration["Supabase:ProjectRef"] ?? "ztpllfixhmifirwadcrs";
        var database = configuration["Supabase:Database"] ?? "postgres";
        var isRender = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RENDER"));
        var usePooler = configuration.GetValue("Supabase:UsePooler", false) || isRender;

        if (usePooler)
        {
            var poolerHost = configuration["Supabase:PoolerHost"]
                ?? Environment.GetEnvironmentVariable("SUPABASE_POOLER_HOST");

            if (string.IsNullOrWhiteSpace(poolerHost))
            {
                throw new InvalidOperationException(
                    "На Render нужен Supabase Pooler (IPv4). " +
                    "Скопируйте PoolerHost из Supabase → Project Settings → Database → Connection string " +
                    "и задайте Supabase:PoolerHost или переменную Supabase__PoolerHost.");
            }

            var poolerPort = configuration["Supabase:PoolerPort"] ?? "5432";

            return Build(
                host: poolerHost,
                port: poolerPort,
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
            var user = string.IsNullOrWhiteSpace(builder.Username) ? "unknown" : builder.Username;
            var host = string.IsNullOrWhiteSpace(builder.Host) ? "unknown" : builder.Host;
            return $"{host}:{builder.Port} as {user}";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string NormalizeConnectionString(string value)
    {
        if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return new NpgsqlConnectionStringBuilder(value) { SslMode = SslMode.Require }.ConnectionString;
        }

        return value;
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
