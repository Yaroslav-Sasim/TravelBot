using Telegram.Bot;
using Telegram.Bot.Types;

var builder = WebApplication.CreateBuilder(args);

var token = builder.Configuration["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured");

var bot = new TelegramBotClient(token);

var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://0.0.0.0:{port}");

app.MapPost("/api/telegram/webhook", async (Update update) =>
{
    var message = update.Message;
    if (message?.Text is null || !message.Text.StartsWith("/start"))
        return Results.Ok();

    await bot.SendMessage(
        chatId: message.Chat.Id,
        text: "Привет, готов путешествовать?");

    return Results.Ok();
});

app.MapGet("/", () => "TravelBot is running");

var webhookBase = builder.Configuration["Telegram:WebhookUrl"]
    ?? Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL");

if (!string.IsNullOrWhiteSpace(webhookBase))
{
    var webhookUrl = $"{webhookBase.TrimEnd('/')}/api/telegram/webhook";
    await bot.SetWebhook(webhookUrl);
    app.Logger.LogInformation("Webhook set to {WebhookUrl}", webhookUrl);
}

app.Run();
