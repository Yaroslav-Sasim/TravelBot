namespace TravelBot.Data.Entities;

public class BookingEntity
{
    public int Id { get; set; }
    public string TourId { get; set; } = null!;
    public DateOnly? DepartureDate { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Comment { get; set; }
    public long TelegramChatId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
