using System.Globalization;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TravelBot.Data;
using TravelBot.Models;
using TravelBot.Services;

namespace TravelBot.Bot;

public sealed class TelegramBotHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly UserSessionStore _sessions;
    private readonly TourRepository _tours;
    private readonly BookingRepository _bookings;
    private readonly KeyboardBuilder _keyboards;
    private readonly AgencyService _agency;
    private readonly ILogger<TelegramBotHandler> _logger;

    public TelegramBotHandler(
        ITelegramBotClient bot,
        UserSessionStore sessions,
        TourRepository tours,
        BookingRepository bookings,
        KeyboardBuilder keyboards,
        AgencyService agency,
        ILogger<TelegramBotHandler> logger)
    {
        _bot = bot;
        _sessions = sessions;
        _tours = tours;
        _bookings = bookings;
        _keyboards = keyboards;
        _agency = agency;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(Update update)
    {
        try
        {
            if (update.CallbackQuery is { } callback)
            {
                await HandleCallbackAsync(callback);
                return;
            }

            if (update.Message is { Text: { } text })
            {
                await HandleMessageAsync(update.Message, text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle update");
        }
    }

    private async Task HandleMessageAsync(Message message, string text)
    {
        var chatId = message.Chat.Id;

        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            _sessions.Reset(chatId);
            await SendMainMenuAsync(chatId, "Привет, готов путешествовать?");
            return;
        }

        var session = _sessions.GetOrCreate(chatId);

        switch (session.Step)
        {
            case UserStep.WaitingSearchQuery:
                session.Step = UserStep.None;
                await SendSearchResultsAsync(chatId, text);
                return;

            case UserStep.WaitingFirstName:
                session.PendingBooking!.FirstName = text.Trim();
                session.Step = UserStep.WaitingLastName;
                await _bot.SendMessage(chatId, "Введите вашу фамилию:");
                return;

            case UserStep.WaitingLastName:
                session.PendingBooking!.LastName = text.Trim();
                session.Step = UserStep.WaitingPhone;
                await _bot.SendMessage(chatId, "Введите ваш телефон:");
                return;

            case UserStep.WaitingPhone:
                session.PendingBooking!.Phone = text.Trim();
                session.Step = UserStep.WaitingBookingDate;
                await SendBookingDatePromptAsync(chatId, session);
                return;

            case UserStep.WaitingBookingDate:
                if (!DateOnly.TryParseExact(text.Trim(), "dd.MM.yyyy", out var date))
                {
                    await _bot.SendMessage(chatId, "Введите дату в формате ДД.ММ.ГГГГ:");
                    return;
                }

                session.PendingBooking!.Date = date;
                session.Step = UserStep.WaitingComment;
                await _bot.SendMessage(chatId, "Оставьте комментарий или отправьте «-», если комментария нет:");
                return;

            case UserStep.WaitingComment:
                session.PendingBooking!.Comment = text.Trim() == "-" ? null : text.Trim();
                await CompleteBookingAsync(chatId, session);
                return;
        }

        await SendMainMenuAsync(chatId, "Выберите раздел в главном меню:");
    }

    private async Task HandleCallbackAsync(CallbackQuery callback)
    {
        var chatId = callback.Message!.Chat.Id;
        var data = callback.Data ?? string.Empty;

        await _bot.AnswerCallbackQuery(callback.Id);

        if (data == "menu:main")
        {
            _sessions.Reset(chatId);
            await SendMainMenuAsync(chatId, "Главное меню:");
            return;
        }

        if (data == "menu:tours")
        {
            await _bot.SendMessage(
                chatId,
                "Выберите направление:",
                replyMarkup: _keyboards.TourDirections());
            return;
        }

        if (data == "menu:calendar")
        {
            await _bot.SendMessage(
                chatId,
                "Выберите месяц:",
                replyMarkup: _keyboards.CalendarMonths());
            return;
        }

        if (data == "menu:search")
        {
            var session = _sessions.GetOrCreate(chatId);
            session.Step = UserStep.WaitingSearchQuery;
            await _bot.SendMessage(
                chatId,
                "Введите ключевые слова для поиска тура.\n" +
                "Можно указать город, направление или название тура.");
            return;
        }

        if (data == "menu:about")
        {
            await _bot.SendMessage(
                chatId,
                _agency.GetAboutText(),
                replyMarkup: _keyboards.AboutActions());
            return;
        }

        if (data == "menu:contact")
        {
            await _bot.SendMessage(
                chatId,
                _agency.GetContactText(),
                replyMarkup: _keyboards.BackToMain());
            return;
        }

        if (data.StartsWith("dir:"))
        {
            var direction = ParseDirection(data["dir:".Length..]);
            var session = _sessions.GetOrCreate(chatId);
            session.LastDirection = direction;
            await SendDirectionToursAsync(chatId, direction);
            return;
        }

        if (data.StartsWith("tour:"))
        {
            var tourId = data["tour:".Length..];
            await SendTourPreviewAsync(chatId, tourId);
            return;
        }

        if (data.StartsWith("detail:"))
        {
            var tourId = data["detail:".Length..];
            await SendTourDetailsAsync(chatId, tourId);
            return;
        }

        if (data.StartsWith("seats:"))
        {
            var tourId = data["seats:".Length..];
            await SendTourSeatsAsync(chatId, tourId);
            return;
        }

        if (data.StartsWith("bookdate:"))
        {
            var parts = data["bookdate:".Length..].Split(':', 2);
            await StartBookingAsync(chatId, parts[0], DateOnly.Parse(parts[1]));
            return;
        }

        if (data.StartsWith("book:"))
        {
            var payload = data["book:".Length..];
            var parts = payload.Split(':', 2);
            var tourId = parts[0];
            DateOnly? date = parts.Length == 2 ? DateOnly.Parse(parts[1]) : null;
            await StartBookingAsync(chatId, tourId, date);
            return;
        }

        if (data.StartsWith("backdir:"))
        {
            var session = _sessions.GetOrCreate(chatId);
            if (session.LastDirection is { } direction)
            {
                await SendDirectionToursAsync(chatId, direction);
                return;
            }

            await _bot.SendMessage(chatId, "Выберите направление:", replyMarkup: _keyboards.TourDirections());
            return;
        }

        if (data.StartsWith("cal:"))
        {
            var month = DateOnly.ParseExact(data["cal:".Length..] + "-01", "yyyy-MM-dd");
            await SendCalendarToursAsync(chatId, month.Year, month.Month);
        }
    }

    private async Task SendMainMenuAsync(long chatId, string text) =>
        await _bot.SendMessage(chatId, text, replyMarkup: _keyboards.MainMenu());

    private async Task SendDirectionToursAsync(long chatId, TourDirection direction)
    {
        var tours = _tours.GetByDirection(direction);
        if (tours.Count == 0)
        {
            await _bot.SendMessage(
                chatId,
                "По этому направлению пока нет туров.",
                replyMarkup: _keyboards.TourDirections());
            return;
        }

        await _bot.SendMessage(
            chatId,
            $"{_tours.GetDirectionTitle(direction)}:\nВыберите тур:",
            replyMarkup: _keyboards.TourList(tours));
    }

    private async Task SendTourPreviewAsync(long chatId, string tourId)
    {
        var tour = _tours.FindById(tourId);
        if (tour is null)
        {
            await _bot.SendMessage(chatId, "Тур не найден.", replyMarkup: _keyboards.BackToMain());
            return;
        }

        var caption = new StringBuilder()
            .AppendLine($"🏷 {tour.Name}")
            .AppendLine($"💰 Стоимость: {tour.Price:N0} ₽")
            .AppendLine($"📅 Даты: {FormatDates(tour.Dates)}")
            .AppendLine($"⏱ Продолжительность: {tour.Duration}")
            .AppendLine($"🚌 Из городов: {string.Join(", ", tour.DepartureCities)}")
            .ToString();

        await _bot.SendPhoto(
            chatId,
            tour.PhotoUrl,
            caption: caption,
            replyMarkup: _keyboards.TourCardActions(tour.Id));
    }

    private async Task SendTourDetailsAsync(long chatId, string tourId)
    {
        var tour = _tours.FindById(tourId);
        if (tour is null)
        {
            await _bot.SendMessage(chatId, "Тур не найден.", replyMarkup: _keyboards.BackToMain());
            return;
        }

        var text = new StringBuilder()
            .AppendLine($"🏷 {tour.Name}")
            .AppendLine($"💰 Стоимость: {tour.Price:N0} ₽")
            .AppendLine($"📅 Даты отправления: {FormatDates(tour.Dates)}")
            .AppendLine($"🚌 Из городов: {string.Join(", ", tour.DepartureCities)}")
            .AppendLine()
            .AppendLine("📋 Программа по дням:")
            .AppendLine(tour.ProgramDescription)
            .AppendLine()
            .AppendLine("✅ Что входит в стоимость:")
            .AppendLine(tour.IncludedInPrice)
            .AppendLine()
            .AppendLine("💳 Доп. платы:")
            .AppendLine(tour.ExtraPayments)
            .ToString();

        await _bot.SendPhoto(
            chatId,
            tour.PhotoUrl,
            caption: text,
            replyMarkup: _keyboards.TourDetailsActions(tour.Id));

        foreach (var image in tour.ProgramImages)
        {
            await _bot.SendPhoto(chatId, image, caption: "Программа тура");
        }
    }

    private async Task SendTourSeatsAsync(long chatId, string tourId)
    {
        var tour = _tours.FindById(tourId);
        if (tour is null)
        {
            await _bot.SendMessage(chatId, "Тур не найден.", replyMarkup: _keyboards.BackToMain());
            return;
        }

        var text = new StringBuilder()
            .AppendLine($"Наличие мест — {tour.Name}")
            .AppendLine()
            .AppendLine(string.Join('\n', tour.Dates.Select(d =>
                $"📅 {d.Date:dd.MM.yyyy} — осталось {d.AvailableSeats} мест")))
            .ToString();

        await _bot.SendMessage(
            chatId,
            text,
            replyMarkup: _keyboards.BookingDateSelection(tour.Id, tour.Dates));
    }

    private async Task SendCalendarToursAsync(long chatId, int year, int month)
    {
        var tours = _tours.GetByMonth(year, month);
        if (tours.Count == 0)
        {
            await _bot.SendMessage(
                chatId,
                "В этом месяце туров нет.",
                replyMarkup: _keyboards.CalendarMonths());
            return;
        }

        var monthName = new DateOnly(year, month, 1).ToString("MMMM yyyy", new CultureInfo("ru-RU"));

        foreach (var tour in tours)
        {
            foreach (var date in tour.Dates.Where(d => d.Date.Year == year && d.Date.Month == month))
            {
                var text =
                    $"📌 {tour.Name}\n" +
                    $"📅 Выезд: {date.Date:dd.MM.yyyy}\n" +
                    $"💰 Стоимость: {tour.Price:N0} ₽";

                await _bot.SendMessage(
                    chatId,
                    text,
                    replyMarkup: _keyboards.CalendarTourActions(tour.Id, date.Date));
            }
        }

        await _bot.SendMessage(
            chatId,
            $"Туры за {monthName}.",
            replyMarkup: _keyboards.CalendarMonths());
    }

    private async Task SendSearchResultsAsync(long chatId, string query)
    {
        var tours = _tours.Search(query);
        if (tours.Count == 0)
        {
            await _bot.SendMessage(
                chatId,
                "По вашему запросу ничего не найдено.",
                replyMarkup: _keyboards.MainMenu());
            return;
        }

        await _bot.SendMessage(chatId, $"Найдено туров: {tours.Count}");
        foreach (var tour in tours)
        {
            await SendTourPreviewAsync(chatId, tour.Id);
        }
    }

    private async Task StartBookingAsync(long chatId, string tourId, DateOnly? selectedDate)
    {
        var tour = _tours.FindById(tourId);
        if (tour is null)
        {
            await _bot.SendMessage(chatId, "Тур не найден.", replyMarkup: _keyboards.BackToMain());
            return;
        }

        var session = _sessions.GetOrCreate(chatId);
        session.PendingBooking = new BookingRequest
        {
            TourId = tourId,
            Date = selectedDate
        };
        session.Step = UserStep.WaitingFirstName;

        await _bot.SendMessage(
            chatId,
            $"Оформление заявки на тур «{tour.Name}».\n\nВведите ваше имя:");
    }

    private async Task SendBookingDatePromptAsync(long chatId, UserSession session)
    {
        var tour = _tours.FindById(session.PendingBooking!.TourId)!;

        if (session.PendingBooking.Date is not null)
        {
            session.Step = UserStep.WaitingComment;
            await _bot.SendMessage(
                chatId,
                $"Дата выбрана: {session.PendingBooking.Date:dd.MM.yyyy}\n" +
                "Оставьте комментарий или отправьте «-», если комментария нет:");
            return;
        }

        await _bot.SendMessage(
            chatId,
            "Выберите дату из доступных или введите её в формате ДД.ММ.ГГГГ:\n\n" +
            string.Join('\n', tour.Dates.Select(d =>
                $"• {d.Date:dd.MM.yyyy} — {d.AvailableSeats} мест")));
    }

    private async Task CompleteBookingAsync(long chatId, UserSession session)
    {
        var booking = session.PendingBooking!;
        var tour = _tours.FindById(booking.TourId)!;

        var summary = new StringBuilder()
            .AppendLine("✅ Заявка принята!")
            .AppendLine($"Тур: {tour.Name}")
            .AppendLine($"Имя: {booking.FirstName} {booking.LastName}")
            .AppendLine($"Телефон: {booking.Phone}")
            .AppendLine($"Дата: {(booking.Date?.ToString("dd.MM.yyyy") ?? "не указана")}")
            .AppendLine($"Комментарий: {booking.Comment ?? "—"}")
            .AppendLine()
            .AppendLine("Менеджер свяжется с вами для подтверждения брони.")
            .ToString();

        _bookings.Save(booking, chatId);

        _logger.LogInformation(
            "New booking: Tour={TourId}, Name={FirstName} {LastName}, Phone={Phone}, Date={Date}",
            booking.TourId,
            booking.FirstName,
            booking.LastName,
            booking.Phone,
            booking.Date);

        _sessions.Reset(chatId);
        await _bot.SendMessage(chatId, summary, replyMarkup: _keyboards.MainMenu());
    }

    private static string FormatDates(IReadOnlyList<TourDate> dates) =>
        string.Join(", ", dates.Select(d => d.Date.ToString("dd.MM.yyyy")));

    private static TourDirection ParseDirection(string value) => value switch
    {
        "nearest" => TourDirection.Nearest,
        "moscow" => TourDirection.Moscow,
        "spb" => TourDirection.SaintPetersburg,
        "karelia" => TourDirection.Karelia,
        "caucasus" => TourDirection.Caucasus,
        "georgia" => TourDirection.Georgia,
        _ => TourDirection.Nearest
    };
}
