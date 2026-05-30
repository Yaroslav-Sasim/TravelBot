namespace TravelBot.Models;

/// <summary>Пользователи, запустившие бота (для авторассылки).</summary>
public class BotUser
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }
    public long TelegramChatId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public DateTime StartedAt { get; set; }
}

