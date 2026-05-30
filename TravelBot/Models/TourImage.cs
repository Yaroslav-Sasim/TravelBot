namespace TravelBot.Models;

public class TourImage
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Caption { get; set; }

    public Tour Tour { get; set; } = null!;
}

