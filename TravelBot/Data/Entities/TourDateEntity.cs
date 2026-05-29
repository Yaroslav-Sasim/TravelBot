namespace TravelBot.Data.Entities;

public class TourDateEntity
{
    public int Id { get; set; }
    public string TourId { get; set; } = null!;
    public TourEntity Tour { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int AvailableSeats { get; set; }
}
