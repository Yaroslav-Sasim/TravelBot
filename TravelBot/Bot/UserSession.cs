using TravelBot.Models;

namespace TravelBot.Bot;

public sealed class UserSession
{
    public UserStep Step { get; set; }
    public TourDirection? LastDirection { get; set; }
    public BookingRequest? PendingBooking { get; set; }
}
