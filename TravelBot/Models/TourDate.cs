namespace TravelBot.Models;

public sealed class TourDate
{
    public DateOnly Date { get; init; }
    public int AvailableSeats { get; init; }
}
