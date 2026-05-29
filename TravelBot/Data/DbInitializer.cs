using TravelBot.Data.Entities;
using TravelBot.Models;

namespace TravelBot.Data;

public static class DbInitializer
{
    public static void Seed(AppDbContext db)
    {
        if (db.Tours.Any())
            return;

        db.Tours.AddRange(
            CreateTour(
                "moscow-weekend",
                "Москва за выходные",
                TourDirection.Moscow,
                "https://images.unsplash.com/photo-1520106212297-deacdd681e48?w=800",
                18900,
                "3 дня / 2 ночи",
                ["Смоленск", "Вязьма"],
                "День 1: прибытие, обзорная экскурсия по центру.\n" +
                "День 2: Кремль, Красная площадь, Арбат.\n" +
                "День 3: свободное время и отъезд.",
                "Проживание, трансфер, экскурсии с гидом.",
                "Обеды, личные расходы, музеи по желанию.",
                [],
                [new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 26)],
                [8, 5]),
            CreateTour(
                "spb-classic",
                "Классический Санкт-Петербург",
                TourDirection.SaintPetersburg,
                "https://images.unsplash.com/photo-1556610961-2feccfc9053e?w=800",
                21500,
                "4 дня / 3 ночи",
                ["Смоленск", "Рославль"],
                "День 1: Невский проспект, Исаакиевский собор.\n" +
                "День 2: Эрмитаж.\n" +
                "День 3: Петергоф.\n" +
                "День 4: отъезд.",
                "Проживание, трансфер, экскурсии.",
                "Питание, билеты в музеи по желанию.",
                [],
                [new DateOnly(2026, 6, 5), new DateOnly(2026, 7, 10)],
                [12, 9]),
            CreateTour(
                "karelia-lakes",
                "Карелия: озёра и водопады",
                TourDirection.Karelia,
                "https://images.unsplash.com/photo-1501785888041-af3ef285b470?w=800",
                24900,
                "5 дней / 4 ночи",
                ["Смоленск", "Москва"],
                "День 1: дорога, размещение.\n" +
                "День 2: Рускеала, мраморный каньон.\n" +
                "День 3: водопады.\n" +
                "День 4: свободная программа.\n" +
                "День 5: возвращение.",
                "Проживание, трансфер, экскурсии.",
                "Питание, сувениры.",
                [],
                [new DateOnly(2026, 6, 18), new DateOnly(2026, 8, 1)],
                [6, 10]),
            CreateTour(
                "caucasus-mountains",
                "Кавказ: горные панорамы",
                TourDirection.Caucasus,
                "https://images.unsplash.com/photo-1464822759023-fed622ff2c3b?w=800",
                27900,
                "6 дней / 5 ночей",
                ["Смоленск", "Москва", "Тула"],
                "День 1: прибытие.\n" +
                "День 2-4: горные маршруты и смотровые площадки.\n" +
                "День 5: дегустация местной кухни.\n" +
                "День 6: отъезд.",
                "Проживание, трансфер, экскурсии.",
                "Обеды, канатные дороги.",
                [],
                [new DateOnly(2026, 7, 3)],
                [7]),
            CreateTour(
                "georgia-bus",
                "Автобусный тур в Грузию",
                TourDirection.Georgia,
                "https://images.unsplash.com/photo-1565008576549-57569a49371d?w=800",
                32900,
                "7 дней / 6 ночей",
                ["Смоленск", "Москва"],
                "День 1: переезд.\n" +
                "День 2: Тбилиси.\n" +
                "День 3: Мцхета.\n" +
                "День 4-5: Кахетия.\n" +
                "День 6: свободный день.\n" +
                "День 7: возвращение.",
                "Проживание, автобус, экскурсии.",
                "Питание, дегустации, личные расходы.",
                [
                    "https://images.unsplash.com/photo-1565008576549-57569a49371d?w=800",
                    "https://images.unsplash.com/photo-1551882547-ff40c63fe5fa?w=800"
                ],
                [new DateOnly(2026, 9, 12), new DateOnly(2026, 10, 3)],
                [4, 6]));

        db.SaveChanges();
    }

    private static TourEntity CreateTour(
        string id,
        string name,
        TourDirection direction,
        string photoUrl,
        decimal price,
        string duration,
        List<string> cities,
        string program,
        string included,
        string extra,
        List<string> images,
        List<DateOnly> dates,
        List<int> seats)
    {
        return new TourEntity
        {
            Id = id,
            Name = name,
            Direction = direction,
            PhotoUrl = photoUrl,
            Price = price,
            Duration = duration,
            DepartureCities = cities,
            ProgramDescription = program,
            IncludedInPrice = included,
            ExtraPayments = extra,
            ProgramImages = images,
            Dates = dates
                .Select((date, index) => new TourDateEntity
                {
                    Date = date,
                    AvailableSeats = seats[index]
                })
                .ToList()
        };
    }
}
