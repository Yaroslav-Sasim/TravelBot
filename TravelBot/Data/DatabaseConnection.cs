using Npgsql;

namespace TravelBot.Data;

public static class DatabaseConnection
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var supabaseConnection = BuildSupabaseConnectionString(configuration);
        if (!string.IsNullOrWhiteSpace(supabaseConnection))
            return supabaseConnection;

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return NormalizePostgresUrl(databaseUrl);

        throw new InvalidOperationException(
            "Строка подключения не настроена. Укажите Supabase:Password / SUPABASE_DB_PASSWORD " +
            "или ConnectionStrings__DefaultConnection.");
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

    private static string? BuildSupabaseConnectionString(IConfiguration configuration)
    {
        var host = configuration["Supabase:Host"];
        if (string.IsNullOrWhiteSpace(host))
            return null;

        var password = configuration["Supabase:Password"]
            ?? Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Укажите пароль Supabase: Supabase__Password или SUPABASE_DB_PASSWORD.");

        var port = configuration["Supabase:Port"] ?? "5432";
        var database = configuration["Supabase:Database"] ?? "postgres";
        var username = configuration["Supabase:Username"] ?? "postgres";

        return
            $"Host={host};" +
            $"Port={port};" +
            $"Database={database};" +
            $"Username={username};" +
            $"Password={password};" +
            "SSL Mode=Require;Trust Server Certificate=true";
    }

    private static string NormalizePostgresUrl(string databaseUrl)
    {
        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            return "postgresql://" + databaseUrl["postgres://".Length..];

        return databaseUrl;
    }
}
