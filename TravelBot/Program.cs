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
    if (update.Message?.Text != "/start")
        return Results.Ok();

    await bot.SendMessage(
        chatId: update.Message.Chat.Id,
        text: "Привет, готов путешествовать?");

    return Results.Ok();
});

app.MapGet("/", () => "TravelBot is running");

app.Run();
