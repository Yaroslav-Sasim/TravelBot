using Npgsql;

namespace TravelBot.Data;

public static class DatabaseConnection
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        // Render/Heroku: DATABASE_URL имеет приоритет над appsettings
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return NormalizePostgresUrl(databaseUrl);

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        throw new InvalidOperationException(
            "Строка подключения не настроена. Привяжите PostgreSQL на Render (DATABASE_URL) " +
            "или укажите ConnectionStrings__DefaultConnection.");
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

    private static string NormalizePostgresUrl(string databaseUrl)
    {
        // Npgsql понимает URI-формат postgres:// и postgresql://
        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            return "postgresql://" + databaseUrl["postgres://".Length..];

        return databaseUrl;
    }
}
