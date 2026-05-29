namespace TravelBot.Data;

public static class DatabaseConnection
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return ParseDatabaseUrl(databaseUrl);

        throw new InvalidOperationException(
            "Строка подключения не настроена. Укажите ConnectionStrings__DefaultConnection или DATABASE_URL.");
    }

    private static string ParseDatabaseUrl(string databaseUrl)
    {
        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':', 2);
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');

            return
                $"Host={uri.Host};" +
                $"Port={port};" +
                $"Database={database};" +
                $"Username={Uri.UnescapeDataString(userInfo[0])};" +
                $"Password={Uri.UnescapeDataString(userInfo[1])};" +
                "SSL Mode=Require;Trust Server Certificate=true";
        }

        return databaseUrl;
    }
}
