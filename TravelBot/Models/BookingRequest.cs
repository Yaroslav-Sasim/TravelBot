namespace TravelBot.Models;

public sealed class BookingRequest
{
    public required string TourId { get; init; }
    public DateOnly? Date { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Comment { get; set; }
}
