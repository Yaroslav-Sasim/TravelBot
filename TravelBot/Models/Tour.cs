namespace TravelBot.Models;

public sealed class Tour
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required TourDirection Direction { get; init; }
    public required string PhotoUrl { get; init; }
    public required decimal Price { get; init; }
    public required string Duration { get; init; }
    public required IReadOnlyList<string> DepartureCities { get; init; }
    public required string ProgramDescription { get; init; }
    public required string IncludedInPrice { get; init; }
    public required string ExtraPayments { get; init; }
    public IReadOnlyList<string> ProgramImages { get; init; } = [];
    public required IReadOnlyList<TourDate> Dates { get; init; }
}
