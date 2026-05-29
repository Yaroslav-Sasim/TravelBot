using Microsoft.EntityFrameworkCore;
using TravelBot.Data.Entities;
using TravelBot.Models;

namespace TravelBot.Data;

public sealed class TourRepository
{
    private readonly AppDbContext _db;

    public TourRepository(AppDbContext db)
    {
        _db = db;
    }

    public Tour? FindById(string id) =>
        MapTour(_db.Tours
            .Include(t => t.Dates)
            .FirstOrDefault(t => t.Id == id));

    public IReadOnlyList<Tour> GetByDirection(TourDirection direction)
    {
        if (direction == TourDirection.Nearest)
        {
            return _db.Tours
                .Include(t => t.Dates)
                .AsEnumerable()
                .OrderBy(t => t.Dates.Min(d => d.Date))
                .Select(MapTour)
                .Where(t => t is not null)
                .Cast<Tour>()
                .ToList();
        }

        return _db.Tours
            .Include(t => t.Dates)
            .Where(t => t.Direction == direction)
            .AsEnumerable()
            .Select(MapTour)
            .Where(t => t is not null)
            .Cast<Tour>()
            .ToList();
    }

    public IReadOnlyList<Tour> Search(string query)
    {
        var terms = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return _db.Tours
            .Include(t => t.Dates)
            .AsEnumerable()
            .Select(MapTour)
            .Where(t => t is not null)
            .Cast<Tour>()
            .Where(tour =>
            {
                var haystack = string.Join(' ',
                    tour.Name,
                    tour.ProgramDescription,
                    string.Join(' ', tour.DepartureCities),
                    GetDirectionTitle(tour.Direction));

                return terms.All(term =>
                    haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
            })
            .ToList();
    }

    public IReadOnlyList<Tour> GetByMonth(int year, int month) =>
        _db.Tours
            .Include(t => t.Dates)
            .Where(t => t.Dates.Any(d => d.Date.Year == year && d.Date.Month == month))
            .AsEnumerable()
            .Select(MapTour)
            .Where(t => t is not null)
            .Cast<Tour>()
            .ToList();

    public string GetDirectionTitle(TourDirection direction) => direction switch
    {
        TourDirection.Nearest => "Ближайшие туры",
        TourDirection.Moscow => "Туры в Москву",
        TourDirection.SaintPetersburg => "Туры в Питер",
        TourDirection.Karelia => "Туры в Карелию",
        TourDirection.Caucasus => "Туры на Кавказ",
        TourDirection.Georgia => "Автобусные туры в Грузию",
        _ => direction.ToString()
    };

    private static Tour? MapTour(TourEntity? entity)
    {
        if (entity is null)
            return null;

        return new Tour
        {
            Id = entity.Id,
            Name = entity.Name,
            Direction = entity.Direction,
            PhotoUrl = entity.PhotoUrl,
            Price = entity.Price,
            Duration = entity.Duration,
            DepartureCities = entity.DepartureCities,
            ProgramDescription = entity.ProgramDescription,
            IncludedInPrice = entity.IncludedInPrice,
            ExtraPayments = entity.ExtraPayments,
            ProgramImages = entity.ProgramImages,
            Dates = entity.Dates
                .OrderBy(d => d.Date)
                .Select(d => new TourDate
                {
                    Date = d.Date,
                    AvailableSeats = d.AvailableSeats
                })
                .ToList()
        };
    }
}
