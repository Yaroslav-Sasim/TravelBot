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
        if (update.Message?.Text != null)
        {
            await _telegramService.SendMessageAsync(
                update.Message.Chat.Id,
                $"Вы написали: {update.Message.Text}"
            );
        }

        return Ok();
    }
}