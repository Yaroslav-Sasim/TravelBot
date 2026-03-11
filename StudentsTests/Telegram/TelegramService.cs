using Telegram.Bot;

public class TelegramService
{
    private readonly ITelegramBotClient _bot;

    public TelegramService(IConfiguration config)
    {
        var token = config["Telegram:BotToken"];
        _bot = new TelegramBotClient(token);
    }

    public async Task SendMessageAsync(long chatId, string text)
    {
        await _bot.SendMessage(
            chatId: chatId,
            text: text
        );
    }
}