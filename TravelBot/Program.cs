using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using TravelBot.Bot;
using TravelBot.Data;
using TravelBot.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var token = builder.Configuration["Telegram:BotToken"];
if (string.IsNullOrWhiteSpace(token))
    throw new InvalidOperationException("Укажите Telegram:BotToken или переменную Telegram__BotToken.");

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

await DatabaseInitializer.ApplyMigrationsAndSeedAsync(app.Services, app.Logger);

app.MapPost("/api/telegram/webhook", async (Update update, TelegramBotHandler handler) =>
{
    await handler.HandleUpdateAsync(update);
    return Results.Ok();
});

app.MapGet("/", () => "TravelBot is running");

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
