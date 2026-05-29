using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using TravelBot.Bot;
using TravelBot.Data;
using TravelBot.Services;

// Render не поддерживает IPv6 — нужен Pooler (шаг 4)
AppContext.SetSwitch("System.Net.DisableIPv6", true);

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var token = builder.Configuration["Telegram:BotToken"];
if (string.IsNullOrWhiteSpace(token))
    throw new InvalidOperationException("Укажите Telegram:BotToken или Telegram__BotToken.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(DatabaseConnection.GetConnectionString(builder.Configuration)));

builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(token));
builder.Services.AddScoped<TourRepository>();
builder.Services.AddScoped<BookingRepository>();
builder.Services.AddSingleton<KeyboardBuilder>();
builder.Services.AddSingleton<AgencyService>();
builder.Services.AddSingleton<UserSessionStore>();
builder.Services.AddScoped<TelegramBotHandler>();

var app = builder.Build();

app.MapPost("/api/telegram/webhook", async (Update update, TelegramBotHandler handler) =>
{
    await handler.HandleUpdateAsync(update);
    return Results.Ok();
});

app.MapGet("/", () => "TravelBot is running");

app.MapGet("/health/db", async (AppDbContext db) =>
{
    try
    {
        return await db.Database.CanConnectAsync()
            ? Results.Ok("Database connected")
            : Results.Problem("Database not connected");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

var connectionString = DatabaseConnection.GetConnectionString(builder.Configuration);
_ = Task.Run(async () =>
{
    try
    {
        await DatabaseInitializer.ApplyMigrationsAndSeedAsync(app.Services, app.Logger, connectionString);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database initialization failed.");
    }
});

var webhookBase = builder.Configuration["Telegram:WebhookUrl"]
    ?? Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL");

if (!string.IsNullOrWhiteSpace(webhookBase))
{
    try
    {
        var bot = app.Services.GetRequiredService<ITelegramBotClient>();
        var webhookUrl = $"{webhookBase.TrimEnd('/')}/api/telegram/webhook";
        await bot.SetWebhook(webhookUrl);
        app.Logger.LogInformation("Webhook set to {WebhookUrl}", webhookUrl);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to set Telegram webhook");
    }
}

app.Run();
