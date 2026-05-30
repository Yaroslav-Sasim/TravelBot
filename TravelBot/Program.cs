using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using TravelBot.Bot;
using TravelBot.Data;
using TravelBot.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var token = builder.Configuration["Telegram:BotToken"]
    ?? builder.Configuration["BotToken"]
    ?? Environment.GetEnvironmentVariable("BOT_TOKEN");

if (string.IsNullOrWhiteSpace(token))
    throw new InvalidOperationException("Укажите Telegram:BotToken или BotToken.");

var adminPassword = builder.Configuration["AdminPassword"] ?? "admin";

var dbConnectionString = DatabaseConnection.GetConnectionString(builder.Configuration);
builder.Logging.AddConsole();
Console.WriteLine($"Database: {DatabaseConnection.GetHostForLogging(dbConnectionString)}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dbConnectionString));

builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(token));
builder.Services.AddSingleton<ConversationState>();
builder.Services.AddSingleton(sp => new ImageStorage(builder.Environment.ContentRootPath));
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<BotApp>(sp => new BotApp(
    sp.GetRequiredService<ITelegramBotClient>(),
    sp.GetRequiredService<AppDbContext>(),
    sp.GetRequiredService<ImageStorage>(),
    sp.GetRequiredService<AdminService>(),
    sp.GetRequiredService<ConversationState>(),
    adminPassword));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(db);

    var adminService = scope.ServiceProvider.GetRequiredService<AdminService>();
    await adminService.EnsureAdminExistsAsync(adminPassword);
}

app.MapPost("/api/telegram/webhook", async (Update update, BotApp botApp) =>
{
    await botApp.HandleUpdateAsync(update, CancellationToken.None);
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
