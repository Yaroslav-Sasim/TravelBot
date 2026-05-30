namespace TravelBot.Models;

public class Admin
{
    public int Id { get; set; }
    public long? TelegramUserId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}


