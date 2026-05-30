namespace TravelBot.Models;

public class TourDirection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<Tour> Tours { get; set; } = new List<Tour>();
}

