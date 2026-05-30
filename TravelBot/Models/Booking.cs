namespace TravelBot.Models;

public class Booking
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public int? TourDateId { get; set; }
    public long TelegramUserId { get; set; }
    public long TelegramChatId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int PlacesCount { get; set; } = 1;
    public string? Comment { get; set; }
    /// <summary>new, confirmed, closed — отображаются как Новая, Подтверждена, Закрыта.</summary>
    public string Status { get; set; } = "new";
    public DateTime CreatedAt { get; set; }

    public Tour Tour { get; set; } = null!;
    public TourDate? TourDate { get; set; }
}

