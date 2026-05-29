namespace TravelBot.Bot;

public enum UserStep
{
    None,
    WaitingSearchQuery,
    WaitingFirstName,
    WaitingLastName,
    WaitingPhone,
    WaitingBookingDate,
    WaitingComment
}
