using Microsoft.EntityFrameworkCore;

namespace TravelBot.Data;

public static class DatabaseInitializer
{
    public static async Task ApplyMigrationsAndSeedAsync(IServiceProvider services, ILogger logger)
    {
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

                logger.LogInformation("Database migrated and seeded.");
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                logger.LogWarning(
                    ex,
                    "Database init attempt {Attempt}/{MaxAttempts} failed.",
                    attempt,
                    maxAttempts);

                if (attempt < maxAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
            }
        }

        throw new InvalidOperationException("Failed to initialize database after multiple attempts.", lastError);
    }
}
