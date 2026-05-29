using Microsoft.EntityFrameworkCore;
using TravelBot.Models;

namespace TravelBot.Data.Entities;

public class TourEntity
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public TourDirection Direction { get; set; }
    public string PhotoUrl { get; set; } = null!;
    public decimal Price { get; set; }
    public string Duration { get; set; } = null!;
    public string ProgramDescription { get; set; } = null!;
    public string IncludedInPrice { get; set; } = null!;
    public string ExtraPayments { get; set; } = null!;
    public List<string> DepartureCities { get; set; } = [];
    public List<string> ProgramImages { get; set; } = [];
    public List<TourDateEntity> Dates { get; set; } = [];
}
