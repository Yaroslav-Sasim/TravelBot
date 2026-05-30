using Microsoft.EntityFrameworkCore;
using TravelBot.Models;

namespace TravelBot.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var tourDates = await db.TourDates.ToListAsync();
        foreach (var td in tourDates)
        {
            var booked = await db.Bookings
                .Where(b => b.TourDateId == td.Id)
                .SumAsync(b => b.PlacesCount);

            if (td.PlacesBooked != booked)
            {
                td.PlacesBooked = booked;
                if (td.PlacesBooked > td.PlacesTotal)
                    td.PlacesBooked = td.PlacesTotal;
            }
        }

        await db.SaveChangesAsync();

        if (await db.TourDirections.AnyAsync())
            return;

        db.TourDirections.AddRange(
            new TourDirection { Name = "Ближайшие туры", SortOrder = 1 },
            new TourDirection { Name = "Туры в Москву", SortOrder = 2 },
            new TourDirection { Name = "Туры в Питер", SortOrder = 3 },
            new TourDirection { Name = "Туры в Карелию", SortOrder = 4 },
            new TourDirection { Name = "Туры на Кавказ", SortOrder = 5 },
            new TourDirection { Name = "Автобусные туры в Грузию", SortOrder = 6 });

        db.PageContents.AddRange(
            new PageContent
            {
                Key = "about_us",
                Content = "TravelBot — турагентство. Заполните текст в админ-панели.",
                UpdatedAt = DateTime.UtcNow
            },
            new PageContent
            {
                Key = "contact_us",
                Content = "Телефон: +7 (900) 000-00-00\nПочта: info@travelbot.ru\nTelegram: @travelbot",
                UpdatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
    }
}
