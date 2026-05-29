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
            "Строка подключения не настроена. Укажите ConnectionStrings:DefaultConnection или DATABASE_URL.");
    }

    private static string ParseDatabaseUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        return
            $"Host={uri.Host};" +
            $"Port={uri.Port};" +
            $"Database={uri.AbsolutePath.TrimStart('/')};" +
            $"Username={Uri.UnescapeDataString(userInfo[0])};" +
            $"Password={Uri.UnescapeDataString(userInfo[1])};" +
            "SSL Mode=Require;Trust Server Certificate=true";
    }
}
