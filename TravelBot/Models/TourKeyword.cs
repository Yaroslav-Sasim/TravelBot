namespace TravelBot.Models;

public class TourKeyword
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public string Keyword { get; set; } = string.Empty;

    public Tour Tour { get; set; } = null!;
}

