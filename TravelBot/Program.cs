using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using TravelBot.Bot;
using TravelBot.Data;
using TravelBot.Services;

var builder = WebApplication.CreateBuilder(args);

var token = builder.Configuration["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured");

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbInitializer.Seed(db);
}

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://0.0.0.0:{port}");

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
    var bot = app.Services.GetRequiredService<ITelegramBotClient>();
    var webhookUrl = $"{webhookBase.TrimEnd('/')}/api/telegram/webhook";
    await bot.SetWebhook(webhookUrl);
    app.Logger.LogInformation("Webhook set to {WebhookUrl}", webhookUrl);
}

app.Run();
