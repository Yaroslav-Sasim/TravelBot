using Microsoft.EntityFrameworkCore;

namespace TravelBot.Data;

public static class DatabaseInitializer
{
    public static async Task ApplyMigrationsAndSeedAsync(
        IServiceProvider services,
        ILogger logger,
        string connectionString)
    {
        var host = DatabaseConnection.GetHostForLogging(connectionString);
        logger.LogInformation("Connecting to database host: {Host}", host);

        const int maxAttempts = 5;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await db.Database.MigrateAsync();
                DbInitializer.Seed(db);

                logger.LogInformation("Database migrated and seeded successfully.");
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                logger.LogWarning(
                    ex,
                    "Database init attempt {Attempt}/{MaxAttempts} failed for host {Host}.",
                    attempt,
                    maxAttempts,
                    host);

                if (attempt < maxAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
            }
        }

        throw new InvalidOperationException(
            $"Failed to connect to database host '{host}'. " +
            "На Render: привяжите PostgreSQL к Web Service (переменная DATABASE_URL).",
            lastError);
    }
}
