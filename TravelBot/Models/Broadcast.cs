namespace TravelBot.Models;

public class Broadcast
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public bool IsActive { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Tour Tour { get; set; } = null!;
}

