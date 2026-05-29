using TravelBot.Data.Entities;
using TravelBot.Models;

namespace TravelBot.Data;

public sealed class BookingRepository
{
    private readonly AppDbContext _db;

    public BookingRepository(AppDbContext db)
    {
        _db = db;
    }

    public void Save(BookingRequest booking, long chatId)
    {
        _db.Bookings.Add(new BookingEntity
        {
            TourId = booking.TourId,
            DepartureDate = booking.Date,
            FirstName = booking.FirstName ?? string.Empty,
            LastName = booking.LastName ?? string.Empty,
            Phone = booking.Phone ?? string.Empty,
            Comment = booking.Comment,
            TelegramChatId = chatId,
            CreatedAtUtc = DateTime.UtcNow
        });

        _db.SaveChanges();
    }
}
