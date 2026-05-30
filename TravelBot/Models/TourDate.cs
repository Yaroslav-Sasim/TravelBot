namespace TravelBot.Models;

public class TourDate
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public DateTime Date { get; set; }
    public int PlacesTotal { get; set; }
    public int PlacesBooked { get; set; }

    public int PlacesLeft => PlacesTotal - PlacesBooked;

    public Tour Tour { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

