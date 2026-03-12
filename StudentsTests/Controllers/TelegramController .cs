using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

[ApiController]
[Route("api/telegram")]
public class TelegramController : ControllerBase
{
    private readonly TelegramService _telegramService;

    public TelegramController(TelegramService telegramService)
    {
        _telegramService = telegramService;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Post([FromBody] Update update)
    {
        if (update?.Message?.Text == null)
            return Ok();

        var chatId = update.Message.Chat.Id;
        var text = update.Message.Text;

        if (text == "/start")
        {
            await _telegramService.SendMessageAsync(
                chatId,
                "Добро пожаловать в систему тестирования 🎓\nВведите ваше имя:"
            );
        }
        else
        {
            await _telegramService.SendMessageAsync(
                chatId,
                $"Вы написали: {text}"
            );
        }

        return Ok();
    }
}