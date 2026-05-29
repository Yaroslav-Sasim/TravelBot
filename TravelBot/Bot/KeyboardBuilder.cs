using System.Globalization;
using Telegram.Bot.Types.ReplyMarkups;
using TravelBot.Models;

namespace TravelBot.Bot;

public sealed class KeyboardBuilder
{
    public InlineKeyboardMarkup MainMenu() => new(
    [
        [InlineKeyboardButton.WithCallbackData("Туры", "menu:tours")],
        [InlineKeyboardButton.WithCallbackData("Календарь туров", "menu:calendar")],
        [InlineKeyboardButton.WithCallbackData("Найти тур", "menu:search")],
        [InlineKeyboardButton.WithCallbackData("О нас", "menu:about")],
        [InlineKeyboardButton.WithCallbackData("Связь с нами", "menu:contact")]
    ]);

    public InlineKeyboardMarkup TourDirections() => new(
    [
        [InlineKeyboardButton.WithCallbackData("Ближайшие туры", "dir:nearest")],
        [InlineKeyboardButton.WithCallbackData("Туры в Москву", "dir:moscow")],
        [InlineKeyboardButton.WithCallbackData("Туры в Питер", "dir:spb")],
        [InlineKeyboardButton.WithCallbackData("Туры в Карелию", "dir:karelia")],
        [InlineKeyboardButton.WithCallbackData("Туры на Кавказ", "dir:caucasus")],
        [InlineKeyboardButton.WithCallbackData("Автобусные туры в Грузию", "dir:georgia")],
        [InlineKeyboardButton.WithCallbackData("← Главное меню", "menu:main")]
    ]);

    public InlineKeyboardMarkup TourList(IReadOnlyList<Tour> tours)
    {
        var rows = tours
            .Select(t => new[] { InlineKeyboardButton.WithCallbackData(t.Name, $"tour:{t.Id}") })
            .ToList();

        rows.Add([InlineKeyboardButton.WithCallbackData("← К направлениям", "menu:tours")]);
        rows.Add([InlineKeyboardButton.WithCallbackData("← Главное меню", "menu:main")]);
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup TourCardActions(string tourId) => new(
    [
        [InlineKeyboardButton.WithCallbackData("Подробнее о туре", $"detail:{tourId}")],
        [InlineKeyboardButton.WithCallbackData("← К списку туров", $"backdir:{tourId}")]
    ]);

    public InlineKeyboardMarkup TourDetailsActions(string tourId) => new(
    [
        [InlineKeyboardButton.WithCallbackData("Кол-во доступных мест", $"seats:{tourId}")],
        [InlineKeyboardButton.WithCallbackData("Забронировать", $"book:{tourId}")],
        [InlineKeyboardButton.WithCallbackData("← К списку туров", $"backdir:{tourId}")]
    ]);

    public InlineKeyboardMarkup CalendarMonths()
    {
        var culture = new CultureInfo("ru-RU");
        var rows = Enumerable.Range(0, 6)
            .Select(offset =>
            {
                var month = DateOnly.FromDateTime(DateTime.Today).AddMonths(offset);
                return new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        month.ToString("MMMM yyyy", culture),
                        $"cal:{month:yyyy-MM}")
                };
            })
            .ToList();

        rows.Add([InlineKeyboardButton.WithCallbackData("← Главное меню", "menu:main")]);
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup CalendarTourActions(string tourId, DateOnly date) => new(
    [
        [InlineKeyboardButton.WithCallbackData("Забронировать", $"book:{tourId}:{date:yyyy-MM-dd}")],
        [InlineKeyboardButton.WithCallbackData("Узнать программу", $"detail:{tourId}")],
        [InlineKeyboardButton.WithCallbackData("← К календарю", "menu:calendar")]
    ]);

    public InlineKeyboardMarkup BookingDateSelection(string tourId, IReadOnlyList<TourDate> dates)
    {
        var rows = dates
            .Select(d => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"{d.Date:dd.MM.yyyy} ({d.AvailableSeats} мест)",
                    $"bookdate:{tourId}:{d.Date:yyyy-MM-dd}")
            })
            .ToList();

        rows.Add([InlineKeyboardButton.WithCallbackData("← Назад", $"detail:{tourId}")]);
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup BackToMain() => new(
        [[InlineKeyboardButton.WithCallbackData("← Главное меню", "menu:main")]]);

    public InlineKeyboardMarkup AboutActions() => new(
    [
        [InlineKeyboardButton.WithCallbackData("Связь с нами", "menu:contact")],
        [InlineKeyboardButton.WithCallbackData("← Главное меню", "menu:main")]
    ]);
}
