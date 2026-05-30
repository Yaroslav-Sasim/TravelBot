using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TravelBot.Data;
using TravelBot.Models;
using TravelBot.Services;

namespace TravelBot.Bot;

public class BotApp
{
    private readonly ITelegramBotClient _client;
    private readonly AppDbContext _db;
    private readonly ImageStorage _images;
    private readonly AdminService _admin;
    private readonly ConversationState _state;
    private readonly string _adminPassword;

    public BotApp(ITelegramBotClient client, AppDbContext db, ImageStorage images, AdminService admin, ConversationState state, string adminPassword)
    {
        _client = client;
        _db = db;
        _images = images;
        _admin = admin;
        _state = state;
        _adminPassword = adminPassword;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        if (update.Message is { } msg)
        {
            await HandleMessageAsync(msg, ct);
            return;
        }
        if (update.CallbackQuery is { } cq)
        {
            await HandleCallbackAsync(cq, ct);
        }
    }

    private async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var text = message.Text?.Trim() ?? "";

        // Обработка фото для админа (включая медиа-группы)
        if (_state.IsAdmin(chatId) && message.Photo != null && message.Photo.Length > 0)
        {
            await HandleAdminPhotoAsync(chatId, message, ct);
            return;
        }
        
        // Обработка медиа-группы для админа (когда отправлено несколько фото сразу)
        if (_state.IsAdmin(chatId) && message.MediaGroupId != null)
        {
            // Медиа-группы обрабатываются через HandleAdminPhotoAsync для каждого сообщения
            // Но нужно сохранить состояние, чтобы не выходить из режима загрузки
            var adminData = _state.Get<AdminConversationState>(chatId);
            if (adminData != null && adminData.Step == AdminStep.WaitTourImage && message.Photo != null && message.Photo.Length > 0)
            {
                await HandleAdminPhotoAsync(chatId, message, ct);
                return;
            }
        }

        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            _state.SetAdmin(chatId, false);
            _state.Set<AdminConversationState>(chatId, null);
            await RegisterUserAndShowMainMenuAsync(chatId, message.From, ct);
            return;
        }

        if (_state.IsAdmin(chatId))
        {
            await HandleAdminMessageAsync(chatId, message, text, ct);
            return;
        }

        // Регистрируем пользователя при любом взаимодействии (если не админ)
        await EnsureUserRegisteredAsync(chatId, message.From, ct);

        var booking = _state.Get<BookingStateData>(chatId);
        if (booking != null)
        {
            await HandleBookingStepAsync(chatId, message, booking, text, ct);
            return;
        }

        if (text.StartsWith("/admin ", StringComparison.OrdinalIgnoreCase))
        {
            var password = text["/admin ".Length..].Trim();
            // Используем проверку пароля из БД вместо пароля из конфига
            if (await _admin.ValidatePasswordAsync(password, ct))
            {
                _state.SetAdmin(chatId, true);
                await _client.SendMessage(chatId, "Вы вошли как администратор.", cancellationToken: ct);
                await ShowAdminMenuAsync(chatId, ct);
            }
            else
                await _client.SendMessage(chatId, "Неверный пароль.", cancellationToken: ct);
            return;
        }

        if (text.Equals("поиск", StringComparison.OrdinalIgnoreCase) || text.StartsWith("найти тур", StringComparison.OrdinalIgnoreCase))
        {
            await _client.SendMessage(chatId, "Введите ключевые слова или название города для поиска тура:", cancellationToken: ct);
            _state.Set(chatId, new SearchState { Active = true });
            return;
        }

        var searchState = _state.Get<SearchState>(chatId);
        if (searchState?.Active == true)
        {
            _state.Set<SearchState>(chatId, null);
            await SearchToursAndSendAsync(chatId, text, ct);
            await ShowMainMenuAsync(chatId, ct);
            return;
        }

        await ShowMainMenuAsync(chatId, ct);
    }

    /// <summary>Регистрирует или обновляет информацию о пользователе при любом взаимодействии с ботом.</summary>
    private async Task EnsureUserRegisteredAsync(long chatId, User? from, CancellationToken ct)
    {
        if (from == null) return;

        var user = await _db.BotUsers.FirstOrDefaultAsync(u => u.TelegramUserId == from.Id, ct);
        if (user == null)
        {
            // Новый пользователь - регистрируем
            _db.BotUsers.Add(new BotUser
            {
                TelegramUserId = from.Id,
                TelegramChatId = chatId,
                FirstName = from.FirstName,
                LastName = from.LastName,
                Username = from.Username,
                StartedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // Обновляем информацию о существующем пользователе (chatId может измениться)
            if (user.TelegramChatId != chatId || 
                user.FirstName != from.FirstName || 
                user.LastName != from.LastName || 
                user.Username != from.Username)
            {
                user.TelegramChatId = chatId;
                user.FirstName = from.FirstName;
                user.LastName = from.LastName;
                user.Username = from.Username;
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task RegisterUserAndShowMainMenuAsync(long chatId, User? from, CancellationToken ct)
    {
        await EnsureUserRegisteredAsync(chatId, from, ct);
        await _client.SendMessage(chatId, "Добро пожаловать! Выберите раздел:", cancellationToken: ct);
        await ShowMainMenuAsync(chatId, ct);
    }

    public async Task ShowMainMenuAsync(long chatId, CancellationToken ct)
    {
        var keys = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("Туры", "menu:tours") },
            new[] { InlineKeyboardButton.WithCallbackData("Календарь туров", "menu:calendar") },
            new[] { InlineKeyboardButton.WithCallbackData("Найти тур", "menu:search") },
            new[] { InlineKeyboardButton.WithCallbackData("Мои заявки / Отменить заявку", "menu:mybookings") },
            new[] { InlineKeyboardButton.WithCallbackData("О нас", "menu:about") },
            new[] { InlineKeyboardButton.WithCallbackData("Связь с нами", "menu:contact") }
        };
        await _client.SendMessage(chatId, "Главное меню:", replyMarkup: new InlineKeyboardMarkup(keys), cancellationToken: ct);
    }

    private async Task ShowUserBookingsAsync(long chatId, CancellationToken ct)
    {
        var bookings = await _db.Bookings
            .Include(b => b.Tour)
            .Include(b => b.TourDate)
            .Where(b => b.TelegramChatId == chatId && (b.Status == "new" || b.Status == "pending" || b.Status == "confirmed"))
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
        if (bookings.Count == 0)
        {
            var backRow = new[] { InlineKeyboardButton.WithCallbackData("← В главное меню", "menu:main") };
            await _client.SendMessage(chatId, "У вас нет активных заявок. Здесь можно отменить заявку после оформления.", replyMarkup: new InlineKeyboardMarkup(backRow), cancellationToken: ct);
            return;
        }
        foreach (var b in bookings)
        {
            var statusText = GetBookingStatusDisplay(b.Status);
            var dateStr = b.TourDate != null ? b.TourDate.Date.ToString("dd.MM.yyyy") : "не указана";
            var text = $"Заявка №{b.Id}\nТур: {b.Tour?.Name ?? "—"}\nДата: {dateStr}\nМест: {b.PlacesCount}\nСтатус: {statusText}";
            var row = new List<InlineKeyboardButton>();
            if (b.Status == "new" || b.Status == "pending")
                row.Add(InlineKeyboardButton.WithCallbackData("Отменить заявку", "booking:cancel:" + b.Id));
            if (row.Count > 0)
                await _client.SendMessage(chatId, text, replyMarkup: new InlineKeyboardMarkup(new[] { row.ToArray() }), cancellationToken: ct);
            else
                await _client.SendMessage(chatId, text, cancellationToken: ct);
        }
        var backOnly = new[] { InlineKeyboardButton.WithCallbackData("← В главное меню", "menu:main") };
        await _client.SendMessage(chatId, "Выше — ваши активные заявки. Подтверждённые отменить нельзя.", replyMarkup: new InlineKeyboardMarkup(backOnly), cancellationToken: ct);
    }

    private async Task HandleCallbackAsync(CallbackQuery cq, CancellationToken ct)
    {
        var chatId = cq.Message!.Chat.Id;
        var data = cq.Data ?? "";

        await _client.AnswerCallbackQuery(cq.Id, cancellationToken: ct);

        if (_state.IsAdmin(chatId))
        {
            await HandleAdminCallbackAsync(chatId, cq, data, ct);
            return;
        }

        // Регистрируем пользователя при любом взаимодействии (если не админ)
        await EnsureUserRegisteredAsync(chatId, cq.From, ct);

        if (data == "menu:main")
        {
            await ShowMainMenuAsync(chatId, ct);
            return;
        }
        if (data == "menu:tours")
        {
            await ShowToursDirectionsAsync(chatId, ct);
            return;
        }
        if (data == "menu:calendar")
        {
            await ShowCalendarMonthsAsync(chatId, ct);
            return;
        }
        if (data == "menu:search")
        {
            await _client.SendMessage(chatId, "Введите ключевые слова или город для поиска:", cancellationToken: ct);
            _state.Set(chatId, new SearchState { Active = true });
            return;
        }
        if (data == "menu:about")
        {
            var page = await _db.PageContents.FirstOrDefaultAsync(p => p.Key == "about_us", ct);
            await _client.SendMessage(chatId, page?.Content ?? "Раздел «О нас» пока не заполнен.", cancellationToken: ct);
            await SendContactButtonAsync(chatId, ct);
            return;
        }
        if (data == "menu:contact")
        {
            var page = await _db.PageContents.FirstOrDefaultAsync(p => p.Key == "contact_us", ct);
            await _client.SendMessage(chatId, page?.Content ?? "Контакты пока не заполнены.", cancellationToken: ct);
            return;
        }
        if (data == "menu:mybookings")
        {
            await ShowUserBookingsAsync(chatId, ct);
            return;
        }

        if (data == "booking:comment:skip")
        {
            var booking = _state.Get<BookingStateData>(chatId);
            if (booking != null && booking.Step == BookingStep.Comment)
            {
                _state.Set<BookingStateData>(chatId, null);
                var userId = await GetTelegramUserIdAsync(chatId, ct) ?? cq.From?.Id ?? 0;
                booking.Comment = null;
                await CompleteBookingAndNotifyAsync(chatId, booking, userId, ct);
            }
            return;
        }
        if (data.StartsWith("booking:cancel:"))
        {
            var idStr = data["booking:cancel:".Length..];
            if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out var cancelBookId))
            {
                var b = await _db.Bookings.Include(x => x.TourDate).FirstOrDefaultAsync(x => x.Id == cancelBookId, ct);
                if (b != null && b.TelegramChatId == chatId && (b.Status == "new" || b.Status == "pending"))
                {
                    if (b.TourDateId.HasValue && b.TourDate != null)
                    {
                        b.TourDate.PlacesBooked -= b.PlacesCount;
                        if (b.TourDate.PlacesBooked < 0) b.TourDate.PlacesBooked = 0;
                    }
                    _db.Bookings.Remove(b);
                    await _db.SaveChangesAsync(ct);
                    var mainMenuBtn = new[] { InlineKeyboardButton.WithCallbackData("В главное меню", "menu:main") };
                    await _client.SendMessage(chatId, "Заявка отменена.", replyMarkup: new InlineKeyboardMarkup(mainMenuBtn), cancellationToken: ct);
                }
                else
                {
                    await _client.SendMessage(chatId, "Эту заявку нельзя отменить (уже обработана или не найдена).", cancellationToken: ct);
                }
            }
            return;
        }

        if (data.StartsWith("dir:"))
        {
            if (int.TryParse(data.AsSpan(4), out var dirId))
                await ShowToursByDirectionAsync(chatId, dirId, ct);
            return;
        }

        if (data.StartsWith("tour:"))
        {
            var parts = data[5..].Split(':');
            if (parts.Length == 1 && int.TryParse(parts[0], out var tourId))
            {
                var choosing = _state.Get<BookingStateData>(chatId);
                if (choosing?.Step == BookingStep.ChoosingDate)
                    _state.Set<BookingStateData>(chatId, null);
                await SendTourDetailsAsync(chatId, tourId, ct);
                return;
            }
            if (parts.Length == 2 && parts[0] == "pl" && int.TryParse(parts[1], out var tid))
            {
                await ShowTourPlacesAsync(chatId, tid, ct);
                return;
            }
            if (parts.Length == 2 && parts[0] == "book" && int.TryParse(parts[1], out var bookTourId))
            {
                await StartBookingAsync(chatId, bookTourId, null, ct);
                return;
            }
        }

        if (data.StartsWith("book:") && data.Length > 5)
        {
            var rest = data[5..];
            var sep = rest.IndexOf(':');
            if (sep > 0 && int.TryParse(rest[..sep], out var tourId) && int.TryParse(rest[(sep + 1)..], out var dateId))
            {
                await StartBookingAsync(chatId, tourId, dateId, ct);
                return;
            }
        }

        if (data.StartsWith("cal:"))
        {
            var monthStr = data[4..];
            if (monthStr.Length >= 7)
                await ShowCalendarToursForMonthAsync(chatId, monthStr, ct);
            return;
        }

        if (data.StartsWith("calt:"))
        {
            var rest = data[5..];
            var idx = rest.IndexOf(':');
            if (idx > 0 && int.TryParse(rest[..idx], out var tourId) && int.TryParse(rest[(idx + 1)..], out var dateId))
            {
                await SendTourDetailsAsync(chatId, tourId, ct);
                return;
            }
        }

        await ShowMainMenuAsync(chatId, ct);
    }

    private async Task ShowToursDirectionsAsync(long chatId, CancellationToken ct)
    {
        var dirs = await _db.TourDirections.OrderBy(d => d.SortOrder).ToListAsync(ct);
        var rows = dirs.Select(d =>
        {
            var buttonText = d.Name.Equals("Ближайшие туры", StringComparison.OrdinalIgnoreCase)
                ? "Ближайшие туры (2 месяца)"
                : d.Name;
            return new[] { InlineKeyboardButton.WithCallbackData(buttonText, "dir:" + d.Id) };
        }).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← Назад", "menu:main") });
        await _client.SendMessage(chatId, "Выберите направление:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    private async Task ShowToursByDirectionAsync(long chatId, int directionId, CancellationToken ct)
    {
        var direction = await _db.TourDirections.FindAsync(new object[] { directionId }, ct);
        if (direction == null)
        {
            await _client.SendMessage(chatId, "Направление не найдено.", cancellationToken: ct);
            await ShowToursDirectionsAsync(chatId, ct);
            return;
        }

        var isNearestTours = direction.Name.Equals("Ближайшие туры", StringComparison.OrdinalIgnoreCase);
        var today = DateTime.UtcNow.Date;
        var twoMonthsLater = today.AddMonths(2);

        var toursQuery = _db.Tours
            .Include(t => t.TourDates.OrderBy(d => d.Date))
            .Where(t => t.TourDirectionId == directionId);

        // Для "Ближайшие туры" показываем только туры с датами в ближайшие 2 месяца (>= сегодня и < сегодня + 2 месяца)
        if (isNearestTours)
        {
            toursQuery = toursQuery.Where(t => t.TourDates.Any(d => d.Date >= today && d.Date < twoMonthsLater && d.PlacesBooked < d.PlacesTotal));
        }

        var tours = await toursQuery
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        if (tours.Count == 0)
        {
            var message = isNearestTours
                ? "На ближайшие 2 месяца туров пока нет. Попробуйте выбрать другое направление или загляните позже."
                : $"По направлению «{direction.Name}» туров пока нет.";
            var backRows = new[] { new[] { InlineKeyboardButton.WithCallbackData("← К направлениям", "menu:tours") } };
            await _client.SendMessage(chatId, message, replyMarkup: new InlineKeyboardMarkup(backRows), cancellationToken: ct);
            return;
        }

        var tourRows = tours.Select(t => new[] { InlineKeyboardButton.WithCallbackData(t.Name, "tour:" + t.Id) }).ToList();
        tourRows.Add(new[] { InlineKeyboardButton.WithCallbackData("← К направлениям", "menu:tours") });
        await _client.SendMessage(chatId, "Выберите тур (откроется полная карточка):", replyMarkup: new InlineKeyboardMarkup(tourRows), cancellationToken: ct);
    }

    private async Task SendTourDetailsAsync(long chatId, int tourId, CancellationToken ct)
    {
        var tour = await _db.Tours
            .Include(t => t.TourDirection)
            .Include(t => t.TourDates.OrderBy(d => d.Date))
            .Include(t => t.TourImages.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(t => t.Id == tourId, ct);
        if (tour == null) return;

        var cities = tour.DepartureCities?.Trim() ?? "—";
        var datesStr = tour.TourDates.Count > 0
            ? string.Join(", ", tour.TourDates.Take(5).Select(d => d.Date.ToString("dd.MM.yyyy"))) + (tour.TourDates.Count > 5 ? "..." : "")
            : "—";
        
        var placesInfo = "";
        if (tour.TourDates.Count > 0)
        {
            var upcomingDates = tour.TourDates.Where(d => d.Date >= DateTime.UtcNow.Date && d.PlacesBooked < d.PlacesTotal).OrderBy(d => d.Date).Take(5).ToList();
            if (upcomingDates.Count > 0)
            {
                var placesLines = upcomingDates.Select(d =>
                {
                    var left = d.PlacesTotal - d.PlacesBooked;
                    if (left < 0) left = 0;
                    return $"{d.Date:dd.MM.yyyy} — свободно {left} из {d.PlacesTotal} мест";
                });
                placesInfo = "\n\n📊 Доступные места:\n" + string.Join("\n", placesLines);
                if (tour.TourDates.Count(d => d.Date >= DateTime.UtcNow.Date && d.PlacesBooked < d.PlacesTotal) > 5)
                    placesInfo += "\n...";
            }
            else
                placesInfo = "\n\n📊 На ближайшие даты мест нет";
        }
        
        var body = $@"{tour.Name}

💰 Стоимость: {tour.Cost ?? "—"}
📅 Даты: {datesStr}
📍 Из каких городов: {cities}{placesInfo}

{tour.Description ?? ""}

Что входит: {tour.Included ?? "—"}
Доп. платы: {tour.ExtraPayments ?? "—"}";

        // Отправляем картинки галереи как медиа-группу (до 10 фото в одной группе)
        if (tour.TourImages.Count > 0)
        {
            var validImages = tour.TourImages
                .Where(img => System.IO.File.Exists(_images.GetFullPath(img.FilePath)))
                .ToList();

            // Группируем по 10 картинок (лимит Telegram для медиа-группы)
            for (int i = 0; i < validImages.Count; i += 10)
            {
                var batch = validImages.Skip(i).Take(10).ToList();
                var mediaGroup = new List<Telegram.Bot.Types.IAlbumInputMedia>();

                // Сначала читаем все файлы в память, чтобы потоки не закрывались раньше времени
                var fileStreams = new List<System.IO.Stream>();
                try
                {
                    for (int j = 0; j < batch.Count; j++)
                    {
                        var img = batch[j];
                        var imgPath = _images.GetFullPath(img.FilePath);
                        var stream = System.IO.File.OpenRead(imgPath);
                        fileStreams.Add(stream);
                        var media = new Telegram.Bot.Types.InputMediaPhoto(InputFile.FromStream(stream, Path.GetFileName(imgPath)));
                        
                        // Подпись только для первой картинки в первой группе, если у неё нет своей подписи
                        if (i == 0 && j == 0 && string.IsNullOrEmpty(img.Caption))
                        {
                            media.Caption = $"Галерея тура «{tour.Name}»";
                        }
                        else if (!string.IsNullOrEmpty(img.Caption))
                        {
                            media.Caption = img.Caption;
                        }
                        
                        mediaGroup.Add(media);
                    }

                    if (mediaGroup.Count > 0)
                    {
                        await _client.SendMediaGroup(chatId, mediaGroup, cancellationToken: ct);
                    }
                }
                finally
                {
                    // Закрываем потоки после отправки
                    foreach (var stream in fileStreams)
                    {
                        stream.Dispose();
                    }
                }

            }
        }
        else if (!string.IsNullOrWhiteSpace(tour.ProgramText))
        {
            await _client.SendMessage(chatId, "Программа по дням:\n" + tour.ProgramText, cancellationToken: ct);
        }

        var keys = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("Забронировать", "tour:book:" + tour.Id) }
        };
        if (tour.TourDates.Any(d => d.Date >= DateTime.UtcNow.Date && d.PlacesBooked < d.PlacesTotal))
        {
            keys.Add(new[] { InlineKeyboardButton.WithCallbackData("Выбрать дату", "tour:pl:" + tour.Id) });
        }
        keys.Add(new[] { InlineKeyboardButton.WithCallbackData("← К списку туров", "dir:" + tour.TourDirectionId) });
        await _client.SendMessage(chatId, body, replyMarkup: new InlineKeyboardMarkup(keys), cancellationToken: ct);
    }

    private async Task ShowTourPlacesAsync(long chatId, int tourId, CancellationToken ct)
    {
        _state.Set(chatId, new BookingStateData { TourId = tourId, TourDateId = null, Step = BookingStep.ChoosingDate });
        var dates = await _db.TourDates.Where(d => d.TourId == tourId && d.Date >= DateTime.UtcNow.Date && d.PlacesBooked < d.PlacesTotal).OrderBy(d => d.Date).Take(15).ToListAsync(ct);
        var rows = dates.Select(d =>
        {
            var left = d.PlacesTotal - d.PlacesBooked;
            if (left < 0) left = 0;
            return new[] { InlineKeyboardButton.WithCallbackData($"{d.Date:dd.MM.yyyy} — свободно {left} из {d.PlacesTotal} мест", "book:" + tourId + ":" + d.Id) };
        }).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Пропустить выбор даты", "tour:book:" + tourId) });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← Назад к туру", "tour:" + tourId) });
        await _client.SendMessage(chatId, "Выберите дату поездки кнопкой, введите дату в формате ДД.ММ.ГГГГ или нажмите «Пропустить выбор даты»:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    private async Task StartBookingAsync(long chatId, int tourId, int? tourDateId, CancellationToken ct)
    {
        // Получаем TelegramUserId из текущего сообщения или из BotUsers
        var userId = await GetTelegramUserIdAsync(chatId, ct);
        if (userId.HasValue)
        {
            // Проверяем, есть ли уже активная заявка от этого пользователя на этот тур
            var existingBooking = await _db.Bookings
                .Include(b => b.TourDate)
                .FirstOrDefaultAsync(b =>
                    b.TourId == tourId &&
                    b.TelegramUserId == userId.Value &&
                    (b.Status == "new" || b.Status == "pending" || b.Status == "confirmed"), ct);
            
            if (existingBooking != null)
            {
                var statusText = GetBookingStatusDisplay(existingBooking.Status);
                var mainMenuButton = new[] { InlineKeyboardButton.WithCallbackData("В главное меню", "menu:main") };
                await _client.SendMessage(chatId,
                    $"⚠️ У вас уже есть активная заявка на этот тур (статус: {statusText}).\n\n" +
                    $"Заявка №{existingBooking.Id}\n" +
                    $"Имя: {existingBooking.FirstName} {existingBooking.LastName}\n" +
                    $"Телефон: {existingBooking.Phone}\n" +
                    $"Количество мест: {existingBooking.PlacesCount}\n" +
                    $"Дата: {(existingBooking.TourDate != null ? existingBooking.TourDate.Date.ToString("dd.MM.yyyy") : "не указана")}\n\n" +
                    $"Дождитесь обработки текущей заявки или обратитесь к администратору.",
                    replyMarkup: new InlineKeyboardMarkup(mainMenuButton),
                    cancellationToken: ct);
                return;
            }
        }

        _state.Set(chatId, new BookingStateData { TourId = tourId, TourDateId = tourDateId, Step = BookingStep.FirstName });
        var dateText = tourDateId.HasValue ? " Дата выбрана." : "";
        await _client.SendMessage(chatId, $"Оставьте заявку.{dateText} Введите ваше **имя**:", cancellationToken: ct);
    }

    private async Task<long?> GetTelegramUserIdAsync(long chatId, CancellationToken ct)
    {
        var botUser = await _db.BotUsers.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);
        return botUser?.TelegramUserId;
    }

    private async Task HandleBookingStepAsync(long chatId, Message message, BookingStateData booking, string text, CancellationToken ct)
    {
        switch (booking.Step)
        {
            case BookingStep.ChoosingDate:
                var dateInput = text.Trim().Replace(" ", "");
                if (dateInput.Equals("пропустить", StringComparison.OrdinalIgnoreCase) || dateInput.Equals("пропустить выбор даты", StringComparison.OrdinalIgnoreCase))
                {
                    booking.TourDateId = null;
                    booking.Step = BookingStep.FirstName;
                    await _client.SendMessage(chatId, "Выбор даты пропущен. Оставьте заявку. Введите ваше **имя**:", cancellationToken: ct);
                    break;
                }
                if (DateTime.TryParse(dateInput, new System.Globalization.CultureInfo("ru-RU"), System.Globalization.DateTimeStyles.None, out var parsedDate))
                {
                    var foundDate = await _db.TourDates.FirstOrDefaultAsync(d =>
                        d.TourId == booking.TourId &&
                        d.Date.Date == parsedDate.Date &&
                        d.Date >= DateTime.UtcNow.Date &&
                        d.PlacesBooked < d.PlacesTotal, ct);
                    if (foundDate != null)
                    {
                        booking.TourDateId = foundDate.Id;
                        booking.Step = BookingStep.FirstName;
                        await _client.SendMessage(chatId, "Дата выбрана. Оставьте заявку. Введите ваше **имя**:", cancellationToken: ct);
                    }
                    else
                        await _client.SendMessage(chatId, "На эту дату нет мест или дата не в списке. Выберите дату кнопкой, введите другую дату (ДД.ММ.ГГГГ) или напишите «пропустить»:", cancellationToken: ct);
                }
                else
                    await _client.SendMessage(chatId, "Неверный формат даты. Введите ДД.ММ.ГГГГ (например 15.03.2026), выберите дату кнопкой или напишите «пропустить»:", cancellationToken: ct);
                break;
            case BookingStep.FirstName:
                booking.FirstName = text;
                booking.Step = BookingStep.LastName;
                await _client.SendMessage(chatId, "Введите **фамилию**:", cancellationToken: ct);
                break;
            case BookingStep.LastName:
                booking.LastName = text;
                booking.Step = BookingStep.Phone;
                await _client.SendMessage(chatId, "Введите **телефон**:", cancellationToken: ct);
                break;
            case BookingStep.Phone:
                booking.Phone = text;
                booking.Step = BookingStep.PlacesCount;
                var maxPlaces = 5;
                var hasDate = false;
                if (booking.TourDateId.HasValue)
                {
                    var selectedDate = await _db.TourDates.FindAsync(new object[] { booking.TourDateId.Value }, ct);
                    if (selectedDate != null)
                    {
                        var onDate = selectedDate.PlacesTotal - selectedDate.PlacesBooked;
                        if (onDate < 1) onDate = 0;
                        maxPlaces = Math.Min(5, onDate);
                        if (maxPlaces < 1) maxPlaces = 1;
                        hasDate = true;
                    }
                }
                var placesPrompt = hasDate
                    ? $"Введите **количество мест** для бронирования (от 1 до 5, на выбранную дату доступно до {maxPlaces}):"
                    : "Введите **количество мест** для бронирования (от 1 до 5):";
                await _client.SendMessage(chatId, placesPrompt, cancellationToken: ct);
                break;
            case BookingStep.PlacesCount:
                if (!int.TryParse(text.Trim(), out var places) || places < 1)
                {
                    await _client.SendMessage(chatId, "Можно забронировать от 1 до 5 мест. Введите корректное количество:", cancellationToken: ct);
                    break;
                }
                if (places > 5)
                {
                    await _client.SendMessage(chatId, "Можно забронировать не более 5 мест за одну заявку. Введите число от 1 до 5:", cancellationToken: ct);
                    break;
                }
                // Проверяем доступность мест, если дата выбрана
                if (booking.TourDateId.HasValue)
                {
                    var selectedDate = await _db.TourDates.FindAsync(new object[] { booking.TourDateId.Value }, ct);
                    if (selectedDate != null)
                    {
                        var available = selectedDate.PlacesTotal - selectedDate.PlacesBooked;
                        if (available < 1)
                        {
                            await _client.SendMessage(chatId, "На выбранную дату больше нет свободных мест. Введите корректное количество или начните бронирование заново и выберите другую дату.", cancellationToken: ct);
                            break;
                        }
                        if (places > available)
                        {
                            await _client.SendMessage(chatId, $"На эту дату свободно только {available} мест. Введите корректное количество (от 1 до {available}):", cancellationToken: ct);
                            break;
                        }
                    }
                }
                booking.PlacesCount = places;
                booking.Step = BookingStep.Comment;
                _state.Set(chatId, booking);
                var noCommentBtn = new[] { InlineKeyboardButton.WithCallbackData("Нет комментария", "booking:comment:skip") };
                await _client.SendMessage(chatId, "Введите **комментарий** или нажмите кнопку:", replyMarkup: new InlineKeyboardMarkup(noCommentBtn), cancellationToken: ct);
                break;
            case BookingStep.Comment:
                booking.Comment = text.Equals("нет", StringComparison.OrdinalIgnoreCase) ? null : text;
                _state.Set<BookingStateData>(chatId, null);
                var userIdMsg = await GetTelegramUserIdAsync(chatId, ct) ?? message.From?.Id ?? 0;
                await CompleteBookingAndNotifyAsync(chatId, booking, userIdMsg, ct);
                break;
        }
    }

    private async Task CompleteBookingAndNotifyAsync(long chatId, BookingStateData booking, long userId, CancellationToken ct)
    {
        var tour = await _db.Tours.FindAsync(new object[] { booking.TourId }, ct);
        TourDate? tourDate = null;
        if (booking.TourDateId.HasValue)
            tourDate = await _db.TourDates.FindAsync(new object[] { booking.TourDateId.Value }, ct);
        var placesCount = booking.PlacesCount > 0 ? booking.PlacesCount : 1;
        var newBooking = new Booking
        {
            TourId = booking.TourId,
            TourDateId = booking.TourDateId,
            TelegramUserId = userId,
            TelegramChatId = chatId,
            FirstName = booking.FirstName ?? "",
            LastName = booking.LastName ?? "",
            Phone = booking.Phone ?? "",
            PlacesCount = placesCount,
            Comment = booking.Comment,
            Status = "new",
            CreatedAt = DateTime.UtcNow
        };
        _db.Bookings.Add(newBooking);
        if (tourDate != null)
        {
            tourDate.PlacesBooked += placesCount;
            if (tourDate.PlacesBooked > tourDate.PlacesTotal)
                tourDate.PlacesBooked = tourDate.PlacesTotal;
        }
        await _db.SaveChangesAsync(ct);
        await _client.SendMessage(chatId, "✅ Заявка принята! Мы свяжемся с вами.", cancellationToken: ct);
        var cancelRow = new[] { InlineKeyboardButton.WithCallbackData("Отменить заявку", "booking:cancel:" + newBooking.Id) };
        await _client.SendMessage(chatId, "Если оформили заявку по ошибке — можете отменить её:", replyMarkup: new InlineKeyboardMarkup(cancelRow), cancellationToken: ct);
        await ShowMainMenuAsync(chatId, ct);
    }

    private async Task ShowCalendarMonthsAsync(long chatId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = new List<InlineKeyboardButton[]>();
        for (var i = 0; i < 6; i++)
        {
            var d = now.AddMonths(i);
            var label = d.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(label, "cal:" + d.ToString("yyyy-MM")) });
        }
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← Назад", "menu:main") });
        await _client.SendMessage(chatId, "Выберите месяц:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    private async Task ShowCalendarToursForMonthAsync(long chatId, string yyyyMm, CancellationToken ct)
    {
        if (!DateTime.TryParse(yyyyMm + "-01", out var monthStart)) return;
        var monthEnd = monthStart.AddMonths(1);

        var dates = await _db.TourDates
            .Include(d => d.Tour)
            .Where(d => d.Date >= monthStart && d.Date < monthEnd && d.PlacesBooked < d.PlacesTotal)
            .OrderBy(d => d.Date)
            .ToListAsync(ct);

        foreach (var g in dates.GroupBy(d => d.TourId))
        {
            var t = g.First().Tour;
            var d = g.First();
            var left = d.PlacesTotal - d.PlacesBooked;
            if (left < 0) left = 0;
            var text = $"{t.Name}\n{d.Date:dd.MM.yyyy} · {t.Cost ?? "—"}\nСвободно мест: {left} из {d.PlacesTotal}";
            var row1 = new[]
            {
                InlineKeyboardButton.WithCallbackData("Программа", "tour:" + t.Id),
                InlineKeyboardButton.WithCallbackData("Забронировать", "book:" + t.Id + ":" + d.Id)
            };
            await _client.SendMessage(chatId, text, replyMarkup: new InlineKeyboardMarkup(row1), cancellationToken: ct);
        }
        if (dates.Count == 0)
            await _client.SendMessage(chatId, "На выбранный месяц туров нет.", cancellationToken: ct);

        var back = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("← Календарь", "menu:calendar"));
        await _client.SendMessage(chatId, "Календарь:", replyMarkup: back, cancellationToken: ct);
    }

    private async Task SearchToursAndSendAsync(long chatId, string query, CancellationToken ct)
    {
        var q = query.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(q))
        {
            await _client.SendMessage(chatId, "Введите ключевые слова.", cancellationToken: ct);
            return;
        }

        var tours = await _db.Tours
            .Include(t => t.TourDirection)
            .Include(t => t.TourDates.OrderBy(d => d.Date))
            .Where(t =>
                t.Name.ToLower().Contains(q) ||
                (t.Description != null && t.Description.ToLower().Contains(q)) ||
                t.TourKeywords.Any(k => k.Keyword.ToLower().Contains(q)))
            .Take(10)
            .ToListAsync(ct);

        if (tours.Count == 0)
        {
            await _client.SendMessage(chatId, "По вашему запросу туров не найдено.", cancellationToken: ct);
            return;
        }

        foreach (var tour in tours)
        {
            var first = tour.TourDates.FirstOrDefault();
            var text = $"{tour.Name}\n{tour.Cost ?? "—"} · {first?.Date:dd.MM.yyyy}";
            var btn = InlineKeyboardButton.WithCallbackData("Подробнее", "tour:" + tour.Id);
            await _client.SendMessage(chatId, text, replyMarkup: new InlineKeyboardMarkup(btn), cancellationToken: ct);
        }
    }

    private async Task SendContactButtonAsync(long chatId, CancellationToken ct)
    {
        await _client.SendMessage(chatId, "Связаться:", replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Связь с нами", "menu:contact")), cancellationToken: ct);
    }

    private class SearchState
    {
        public bool Active { get; set; }
    }

    #region Admin

    private async Task HandleAdminMessageAsync(long chatId, Message message, string text, CancellationToken ct)
    {
        if (text.Equals("/exit", StringComparison.OrdinalIgnoreCase) || text.Equals("выход", StringComparison.OrdinalIgnoreCase))
        {
            _state.SetAdmin(chatId, false);
            _state.Set<AdminConversationState>(chatId, null);
            await _client.SendMessage(chatId, "Вы вышли из режима управления ботом.", cancellationToken: ct);
            // Показываем главное меню пользователя (как при /start)
            await ShowMainMenuAsync(chatId, ct);
            return;
        }

        var adminData = _state.Get<AdminConversationState>(chatId);
        if (adminData != null)
        {
            await HandleAdminConversationAsync(chatId, message, text, adminData, ct);
            return;
        }

        await ShowAdminMenuAsync(chatId, ct);
    }

    private async Task HandleAdminConversationAsync(long chatId, Message message, string text, AdminConversationState adminData, CancellationToken ct)
    {
        // Общая обработка "отмена" только если не в процессе загрузки фото
        if (text.Equals("отмена", StringComparison.OrdinalIgnoreCase) && 
            adminData.Step != AdminStep.WaitTourImage)
        {
            _state.Set<AdminConversationState>(chatId, null);
            await _client.SendMessage(chatId, "Отменено.", cancellationToken: ct);
            await ShowAdminMenuAsync(chatId, ct);
            return;
        }

        switch (adminData.Step)
        {
            case AdminStep.WaitPageContent:
                if (text.Equals("отмена", StringComparison.OrdinalIgnoreCase))
                {
                    _state.Set<AdminConversationState>(chatId, null);
                    await _client.SendMessage(chatId, "❌ Редактирование страницы отменено.", cancellationToken: ct);
                    await ShowAdminMenuAsync(chatId, ct);
                    break;
                }
                var page = await _db.PageContents.FirstOrDefaultAsync(p => p.Key == adminData.TargetKey, ct);
                if (page == null)
                {
                    page = new PageContent { Key = adminData.TargetKey, Content = "", UpdatedAt = DateTime.UtcNow };
                    _db.PageContents.Add(page);
                }
                page.Content = text;
                page.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                _state.Set<AdminConversationState>(chatId, null);
                await _client.SendMessage(chatId, "Сохранено.", cancellationToken: ct);
                await ShowAdminMenuAsync(chatId, ct);
                break;
            case AdminStep.WaitDirectionName:
                if (text.Equals("отмена", StringComparison.OrdinalIgnoreCase))
                {
                    _state.Set<AdminConversationState>(chatId, null);
                    if (adminData.IsEdit && adminData.DirectionId.HasValue)
                    {
                        await _client.SendMessage(chatId, "❌ Редактирование направления отменено.", cancellationToken: ct);
                        await AdminListDirectionsAsync(chatId, ct);
                    }
                    else
                    {
                        await _client.SendMessage(chatId, "❌ Добавление направления отменено.", cancellationToken: ct);
                        await ShowAdminMenuAsync(chatId, ct);
                    }
                    break;
                }
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (adminData.IsEdit && adminData.DirectionId.HasValue)
                    {
                        var cancelEditDirButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:dir:edit:cancel:" + adminData.DirectionId.Value) };
                        await _client.SendMessage(chatId, "Название не может быть пустым. Введите название направления:", 
                            replyMarkup: new InlineKeyboardMarkup(cancelEditDirButton), cancellationToken: ct);
                    }
                    else
                    {
                        var cancelDirButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:dir:cancel") };
                        await _client.SendMessage(chatId, "Название не может быть пустым. Введите название направления:", 
                            replyMarkup: new InlineKeyboardMarkup(cancelDirButton), cancellationToken: ct);
                    }
                    break;
                }
                if (adminData.IsEdit && adminData.DirectionId.HasValue)
                {
                    var dir = await _db.TourDirections.FindAsync(new object[] { adminData.DirectionId.Value }, ct);
                    if (dir != null) { dir.Name = text.Trim(); await _db.SaveChangesAsync(ct); }
                    await _client.SendMessage(chatId, "Направление обновлено.", cancellationToken: ct);
                    await AdminListDirectionsAsync(chatId, ct);
                }
                else
                {
                    _db.TourDirections.Add(new TourDirection { Name = text.Trim(), SortOrder = adminData.SortOrder });
                    await _db.SaveChangesAsync(ct);
                    await _client.SendMessage(chatId, "Направление добавлено.", cancellationToken: ct);
                    await AdminListDirectionsAsync(chatId, ct);
                }
                _state.Set<AdminConversationState>(chatId, null);
                break;
            case AdminStep.WaitTourField:
                if (text.Equals("готово", StringComparison.OrdinalIgnoreCase))
                {
                    _state.Set<AdminConversationState>(chatId, null);
                    await _client.SendMessage(chatId, "Редактирование завершено.", cancellationToken: ct);
                    await ShowAdminMenuAsync(chatId, ct);
                    break;
                }
                if (text.Equals("отмена", StringComparison.OrdinalIgnoreCase))
                {
                    adminData.TourField = null;
                    _state.Set(chatId, adminData);
                    await ShowTourEditButtonsAsync(chatId, adminData.TourId!.Value, "Выберите поле для изменения:", ct);
                    break;
                }
                if (string.IsNullOrEmpty(adminData.TourField))
                {
                    await ShowTourEditButtonsAsync(chatId, adminData.TourId!.Value, "Выберите поле кнопкой:", ct);
                    break;
                }
                await SaveAdminTourFieldAsync(chatId, adminData, text, ct);
                break;
            case AdminStep.WaitTourDate:
                if (text.Equals("отмена", StringComparison.OrdinalIgnoreCase))
                {
                    _state.Set<AdminConversationState>(chatId, null);
                    await ShowTourEditButtonsAsync(chatId, adminData.TourId!.Value, "Редактирование тура:", ct);
                    break;
                }
                if (DateTime.TryParse(text.Trim(), new System.Globalization.CultureInfo("ru-RU"), System.Globalization.DateTimeStyles.None, out var parsedDate))
                {
                    if (!adminData.TourId.HasValue)
                    {
                        await _client.SendMessage(chatId, "Ошибка: не выбран тур. Вернитесь в меню редактирования.", cancellationToken: ct);
                        break;
                    }
                    adminData.TempDate = parsedDate;
                    adminData.Step = AdminStep.WaitTourDatePlaces;
                    _state.Set(chatId, adminData);
                    var cancelPlacesButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:tour:date:cancel:" + adminData.TourId.Value) };
                    await _client.SendMessage(chatId, "Введите количество мест на эту дату:", 
                        replyMarkup: new InlineKeyboardMarkup(cancelPlacesButton), cancellationToken: ct);
                }
                else
                {
                    if (!adminData.TourId.HasValue)
                    {
                        await _client.SendMessage(chatId, "Ошибка: не выбран тур. Вернитесь в меню редактирования.", cancellationToken: ct);
                        break;
                    }
                    var cancelDateButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:tour:date:cancel:" + adminData.TourId.Value) };
                    await _client.SendMessage(chatId, "Неверный формат. Введите дату ДД.ММ.ГГГГ (например 15.03.2026):", 
                        replyMarkup: new InlineKeyboardMarkup(cancelDateButton), cancellationToken: ct);
                }
                break;
            case AdminStep.WaitTourDatePlaces:
                if (!adminData.TourId.HasValue)
                {
                    await _client.SendMessage(chatId, "Ошибка: не выбран тур. Вернитесь в меню редактирования.", cancellationToken: ct);
                    _state.Set<AdminConversationState>(chatId, null);
                    await ShowAdminMenuAsync(chatId, ct);
                    break;
                }
                if (text.Equals("отмена", StringComparison.OrdinalIgnoreCase))
                {
                    _state.Set<AdminConversationState>(chatId, null);
                    await ShowTourEditButtonsAsync(chatId, adminData.TourId.Value, "Редактирование тура:", ct);
                    break;
                }
                if (int.TryParse(text.Trim(), out var places) && places > 0 && adminData.TempDate.HasValue)
                {
                    _db.TourDates.Add(new TourDate { TourId = adminData.TourId.Value, Date = adminData.TempDate.Value.Date, PlacesTotal = places, PlacesBooked = 0 });
                    await _db.SaveChangesAsync(ct);
                    _state.Set<AdminConversationState>(chatId, null);
                    await _client.SendMessage(chatId, "Дата добавлена. Тур появится в календаре.", cancellationToken: ct);
                    await AdminListTourDatesAsync(chatId, adminData.TourId.Value, ct);
                }
                else
                {
                    var cancelPlacesButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:tour:date:cancel:" + adminData.TourId.Value) };
                    await _client.SendMessage(chatId, "Введите целое число (количество мест):", 
                        replyMarkup: new InlineKeyboardMarkup(cancelPlacesButton), cancellationToken: ct);
                }
                break;
            case AdminStep.WaitAdminOldPassword:
                if (text.Equals("отмена", StringComparison.OrdinalIgnoreCase))
                {
                    _state.Set<AdminConversationState>(chatId, null);
                    await _client.SendMessage(chatId, "Смена пароля отменена.", cancellationToken: ct);
                    await ShowAdminMenuAsync(chatId, ct);
                    break;
                }
                if (await _admin.ValidatePasswordAsync(text, ct))
                {
                    adminData.TempPassword = text;
                    adminData.Step = AdminStep.WaitAdminNewPassword;
                    _state.Set(chatId, adminData);
                    var cancelNewPasswordButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:password:cancel") };
                    await _client.SendMessage(chatId, "Текущий пароль верный. Введите новый пароль:", 
                        replyMarkup: new InlineKeyboardMarkup(cancelNewPasswordButton), cancellationToken: ct);
                }
                else
                {
                    var cancelPasswordButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:password:cancel") };
                    await _client.SendMessage(chatId, "Неверный пароль. Введите текущий пароль ещё раз:", 
                        replyMarkup: new InlineKeyboardMarkup(cancelPasswordButton), cancellationToken: ct);
                }
                break;
            case AdminStep.WaitAdminNewPassword:
                if (text.Equals("отмена", StringComparison.OrdinalIgnoreCase))
                {
                    _state.Set<AdminConversationState>(chatId, null);
                    await _client.SendMessage(chatId, "Смена пароля отменена.", cancellationToken: ct);
                    await ShowAdminMenuAsync(chatId, ct);
                    break;
                }
                if (string.IsNullOrWhiteSpace(text) || text.Length < 4)
                {
                    await _client.SendMessage(chatId, "Пароль должен быть не короче 4 символов. Введите новый пароль:", cancellationToken: ct);
                    break;
                }
                if (adminData.TempPassword != null && await _admin.ChangePasswordAsync(adminData.TempPassword, text.Trim(), ct))
                {
                    _state.Set<AdminConversationState>(chatId, null);
                    await _client.SendMessage(chatId, "Пароль успешно изменён. Используйте новый пароль при следующем входе.", cancellationToken: ct);
                    await ShowAdminMenuAsync(chatId, ct);
                }
                else
                {
                    _state.Set<AdminConversationState>(chatId, null);
                    await _client.SendMessage(chatId, "Не удалось сменить пароль. Попробуйте снова из меню.", cancellationToken: ct);
                    await ShowAdminMenuAsync(chatId, ct);
                }
                break;
            case AdminStep.WaitTourImage:
                // Поддерживаем текстовые команды для обратной совместимости, но рекомендуем использовать кнопки
                if (text.Equals("отмена", StringComparison.OrdinalIgnoreCase) || text.Equals("❌ отмена", StringComparison.OrdinalIgnoreCase))
                {
                    adminData.Step = AdminStep.WaitTourField;
                    adminData.TourField = null;
                    _state.Set(chatId, adminData);
                    await _client.SendMessage(chatId, "❌ Загрузка картинок отменена.", cancellationToken: ct);
                    await ShowTourEditButtonsAsync(chatId, adminData.TourId!.Value, "Выберите поле для изменения:", ct);
                    break;
                }
                if (text.Equals("готово", StringComparison.OrdinalIgnoreCase) || text.Equals("✅ готово", StringComparison.OrdinalIgnoreCase))
                {
                    adminData.Step = AdminStep.WaitTourField;
                    adminData.TourField = null;
                    _state.Set(chatId, adminData);
                    await _client.SendMessage(chatId, "✅ Загрузка картинок завершена.", cancellationToken: ct);
                    await ShowTourEditButtonsAsync(chatId, adminData.TourId!.Value, "Выберите поле для изменения:", ct);
                    break;
                }
                // Если отправлен текст (не фото), показываем подсказку с кнопками
                if (adminData.TourId.HasValue)
                {
                    var hintButtons = new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "a:img:ready:" + adminData.TourId.Value) },
                        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:img:cancel:" + adminData.TourId.Value) }
                    };
                    await _client.SendMessage(chatId, 
                        "📷 Отправьте фото для добавления в галерею тура.\n\n" +
                        "Можно отправить несколько фото сразу (альбомом) или по одному.\n\n" +
                        "Нажмите «Готово» для завершения:", 
                        replyMarkup: new InlineKeyboardMarkup(hintButtons),
                        cancellationToken: ct);
                }
                else
                {
                    await _client.SendMessage(chatId, "Ошибка: не выбран тур. Вернитесь в меню редактирования.", cancellationToken: ct);
                }
                break;
            default:
                _state.Set<AdminConversationState>(chatId, null);
                await ShowAdminMenuAsync(chatId, ct);
                break;
        }
    }

    private async Task HandleAdminPhotoAsync(long chatId, Message message, CancellationToken ct)
    {
        var adminData = _state.Get<AdminConversationState>(chatId);
        if (adminData == null || !adminData.TourId.HasValue)
        {
            await _client.SendMessage(chatId, "Сначала выберите тур для редактирования.", cancellationToken: ct);
            return;
        }

        if (message.Photo == null || message.Photo.Length == 0)
        {
            await _client.SendMessage(chatId, "Не удалось получить фото. Попробуйте ещё раз.", cancellationToken: ct);
            return;
        }

        // Получаем фото наибольшего размера
        var photo = message.Photo.OrderByDescending(p => p.FileSize).First();
        var file = await _client.GetFile(photo.FileId, ct);
        if (file.FilePath == null)
        {
            await _client.SendMessage(chatId, "Не удалось загрузить фото. Попробуйте ещё раз.", cancellationToken: ct);
            return;
        }

        await using var stream = new MemoryStream();
        await _client.DownloadFile(file.FilePath, stream, ct);
        stream.Position = 0;

        var fileName = file.FilePath.Split('/').LastOrDefault() ?? "photo.jpg";
        var relativePath = await _images.SaveAsync(stream, fileName, ct);

        var tour = await _db.Tours.FindAsync(new object[] { adminData.TourId.Value }, ct);
        if (tour == null)
        {
            await _client.SendMessage(chatId, "Тур не найден.", cancellationToken: ct);
            return;
        }

        if (adminData.Step == AdminStep.WaitTourImage)
        {
            // Добавляем картинку в галерею
            var maxOrder = await _db.TourImages.Where(ti => ti.TourId == adminData.TourId.Value).MaxAsync(ti => (int?)ti.SortOrder, ct) ?? -1;
            _db.TourImages.Add(new TourImage
            {
                TourId = adminData.TourId.Value,
                FilePath = relativePath,
                SortOrder = maxOrder + 1,
                Caption = message.Caption
            });
            tour.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            
            // Не выходим из режима загрузки, чтобы можно было отправить ещё картинки
            // Состояние остается WaitTourImage
            _state.Set(chatId, adminData);
            
            // Если это не медиа-группа (MediaGroupId == null), значит отправлена одна картинка
            // В этом случае можно показать сообщение и предложить продолжить или закончить
            if (message.MediaGroupId == null)
            {
                var readyButtons = new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "a:img:ready:" + adminData.TourId.Value) },
                    new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:img:cancel:" + adminData.TourId.Value) }
                };
                await _client.SendMessage(chatId, 
                    "✅ Картинка добавлена в галерею тура.\n\nОтправьте ещё фото для добавления в галерею или нажмите «Готово» для завершения.", 
                    replyMarkup: new InlineKeyboardMarkup(readyButtons),
                    cancellationToken: ct);
            }
            // Если это медиа-группа, просто сохраняем без сообщения (чтобы не спамить)
        }
        else
        {
            await _client.SendMessage(chatId, "Сначала выберите действие с картинками в меню редактирования тура.", cancellationToken: ct);
        }
    }

    private async Task SaveAdminTourFieldAsync(long chatId, AdminConversationState adminData, string text, CancellationToken ct)
    {
        var tour = await _db.Tours.FindAsync(new object[] { adminData.TourId!.Value }, ct);
        if (tour == null) { _state.Set<AdminConversationState>(chatId, null); return; }

        switch (adminData.TourField)
        {
            case "name": tour.Name = text; break;
            case "cost": tour.Cost = text; break;
            case "cities": tour.DepartureCities = text; break;
            case "description": tour.Description = text; break;
            case "program": tour.ProgramText = text; break;
            case "included": tour.Included = text; break;
            case "extra": tour.ExtraPayments = text; break;
        }
        tour.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        adminData.TourField = null;
        _state.Set(chatId, adminData);
        await ShowTourEditButtonsAsync(chatId, adminData.TourId!.Value, "Поле сохранено. Выберите поле для изменения:", ct);
    }

    private async Task ShowTourEditButtonsAsync(long chatId, int tourId, string message, CancellationToken ct)
    {
        var keys = new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Название", "a:tour:f:" + tourId + ":name") },
            new[] { InlineKeyboardButton.WithCallbackData("Стоимость", "a:tour:f:" + tourId + ":cost") },
            new[] { InlineKeyboardButton.WithCallbackData("Из каких городов", "a:tour:f:" + tourId + ":cities") },
            new[] { InlineKeyboardButton.WithCallbackData("Даты тура", "a:tour:dates:" + tourId) },
            new[] { InlineKeyboardButton.WithCallbackData("Картинки тура", "a:tour:images:" + tourId) },
            new[] { InlineKeyboardButton.WithCallbackData("Описание", "a:tour:f:" + tourId + ":description") },
            new[] { InlineKeyboardButton.WithCallbackData("Программа по дням", "a:tour:f:" + tourId + ":program") },
            new[] { InlineKeyboardButton.WithCallbackData("Что входит", "a:tour:f:" + tourId + ":included") },
            new[] { InlineKeyboardButton.WithCallbackData("Доп. платы", "a:tour:f:" + tourId + ":extra") },
            new[] { InlineKeyboardButton.WithCallbackData("Готово", "a:tour:done:" + tourId) }
        };
        await _client.SendMessage(chatId, message, replyMarkup: new InlineKeyboardMarkup(keys), cancellationToken: ct);
    }

    private async Task ShowAdminMenuAsync(long chatId, CancellationToken ct)
    {
        var keys = new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Направления туров", "a:dirs") },
            new[] { InlineKeyboardButton.WithCallbackData("Туры", "a:tours") },
            new[] { InlineKeyboardButton.WithCallbackData("Тексты «О нас» и «Контакты»", "a:pages") },
            new[] { InlineKeyboardButton.WithCallbackData("Заявки на бронирование", "a:bookings") },
            new[] { InlineKeyboardButton.WithCallbackData("Рассылка по туру", "a:broadcast") },
            new[] { InlineKeyboardButton.WithCallbackData("Сменить пароль входа", "a:password") },
            new[] { InlineKeyboardButton.WithCallbackData("Выйти из админ-режима", "a:exit") }
        };
        await _client.SendMessage(chatId, "Управление ботом. Выберите раздел:", replyMarkup: new InlineKeyboardMarkup(keys), cancellationToken: ct);
    }

    private static string GetBookingStatusDisplay(string status)
    {
        return status switch
        {
            "new" => "Новая",
            "pending" => "Новая",
            "confirmed" => "Подтверждена",
            "closed" => "Закрыта",
            "cancelled" => "Закрыта",
            _ => status
        };
    }

    private async Task HandleAdminCallbackAsync(long chatId, CallbackQuery cq, string data, CancellationToken ct)
    {
        if (data == "a:menu")
        {
            await ShowAdminMenuAsync(chatId, ct);
            return;
        }
        if (data == "a:exit")
        {
            _state.SetAdmin(chatId, false);
            _state.Set<AdminConversationState>(chatId, null);
            await _client.SendMessage(chatId, "Вы вышли из режима управления ботом.", cancellationToken: ct);
            // Показываем главное меню пользователя (как при /start)
            await ShowMainMenuAsync(chatId, ct);
            return;
        }
        if (data == "a:dirs")
        {
            await AdminListDirectionsAsync(chatId, ct);
            return;
        }
        if (data.StartsWith("a:dir:add"))
        {
            _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitDirectionName, SortOrder = await _db.TourDirections.CountAsync(ct) + 1 });
            var cancelDirButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:dir:cancel") };
            await _client.SendMessage(chatId, "Введите название направления туров (например: Туры в Москву):", 
                replyMarkup: new InlineKeyboardMarkup(cancelDirButton), cancellationToken: ct);
            return;
        }
        if (data.StartsWith("a:dir:edit:") && data.Length > 11 && int.TryParse(data.AsSpan(11), out var dirId))
        {
            var dir = await _db.TourDirections.FindAsync(new object[] { dirId }, ct);
            if (dir != null)
            {
                _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitDirectionName, DirectionId = dirId, IsEdit = true });
                var cancelEditDirButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:dir:edit:cancel:" + dirId) };
                await _client.SendMessage(chatId, "Введите новое название направления:", 
                    replyMarkup: new InlineKeyboardMarkup(cancelEditDirButton), cancellationToken: ct);
            }
            return;
        }
        if (data.StartsWith("a:dir:del:") && data.Length > 10 && int.TryParse(data.AsSpan(10), out var dId))
        {
            var dir = await _db.TourDirections.Include(d => d.Tours).FirstOrDefaultAsync(d => d.Id == dId, ct);
            if (dir != null)
            {
                _db.TourDirections.Remove(dir);
                await _db.SaveChangesAsync(ct);
                await _client.SendMessage(chatId, "Направление удалено.", cancellationToken: ct);
            }
            await ShowAdminMenuAsync(chatId, ct);
            return;
        }

        if (data == "a:tours")
        {
            await AdminListToursAsync(chatId, ct);
            return;
        }
        if (data == "a:tour:add")
        {
            var dirs = await _db.TourDirections.OrderBy(d => d.SortOrder).ToListAsync(ct);
            var rows = dirs.Select(d => new[] { InlineKeyboardButton.WithCallbackData(d.Name, "a:tour:new:" + d.Id) }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← К списку туров", "a:tours") });
            await _client.SendMessage(chatId, "Выберите направление (категорию), в которое добавить новый тур:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
            return;
        }
        if (data.StartsWith("a:tour:new:") && data.Length > 11 && int.TryParse(data.AsSpan(11), out var dirIdNew))
        {
            _db.Tours.Add(new Tour { TourDirectionId = dirIdNew, Name = "Новый тур", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync(ct);
            var newTour = await _db.Tours.OrderByDescending(t => t.Id).FirstAsync(ct);
            _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitTourField, TourId = newTour.Id, TourField = null });
            await ShowTourEditButtonsAsync(chatId, newTour.Id, "Новый тур создан. Выберите, что заполнить или изменить:", ct);
            return;
        }
        if (data.StartsWith("a:tour:edit:") && data.Length > 12 && int.TryParse(data.AsSpan(12), out var tourIdEdit))
        {
            _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitTourField, TourId = tourIdEdit, TourField = null });
            await ShowTourEditButtonsAsync(chatId, tourIdEdit, "Редактирование тура. Выберите, что изменить:", ct);
            return;
        }
        if (data.StartsWith("a:tour:f:") && data.Length > 9)
        {
            var parts = data["a:tour:f:".Length..].Split(':', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out var fid) && !string.IsNullOrEmpty(parts[1]))
            {
                var field = parts[1];
                var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"] = "название", ["cost"] = "стоимость", ["cities"] = "из каких городов",
                    ["description"] = "описание", ["program"] = "программу по дням", ["included"] = "что входит", ["extra"] = "доп. платы"
                };
                _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitTourField, TourId = fid, TourField = field });
                var label = labels.TryGetValue(field, out var l) ? l : field;
                var cancelButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:tour:field:cancel:" + fid) };
                await _client.SendMessage(chatId, $"Введите значение для поля «{label}»:", 
                    replyMarkup: new InlineKeyboardMarkup(cancelButton), cancellationToken: ct);
            }
            return;
        }
        if (data.StartsWith("a:tour:done:") && data.Length > 12 && int.TryParse(data.AsSpan(12), out var doneTourId))
        {
            _state.Set<AdminConversationState>(chatId, null);
            await _client.SendMessage(chatId, "Редактирование завершено.", cancellationToken: ct);
            await ShowAdminMenuAsync(chatId, ct);
            return;
        }
        if (data.StartsWith("a:tour:dates:") && data.Length > 13 && int.TryParse(data.AsSpan(13), out var datesTourId))
        {
            await AdminListTourDatesAsync(chatId, datesTourId, ct);
            return;
        }
        if (data.StartsWith("a:tour:date:cancel:"))
        {
            var cancelDateIdStr = data["a:tour:date:cancel:".Length..];
            if (!string.IsNullOrEmpty(cancelDateIdStr) && int.TryParse(cancelDateIdStr, out var cancelDateTourId))
            {
                _state.Set<AdminConversationState>(chatId, null);
                await _client.SendMessage(chatId, "❌ Добавление даты отменено.", cancellationToken: ct);
                await ShowTourEditButtonsAsync(chatId, cancelDateTourId, "Редактирование тура:", ct);
                return;
            }
        }
        if (data.StartsWith("a:tour:images:"))
        {
            var imagesIdStr = data["a:tour:images:".Length..];
            if (!string.IsNullOrEmpty(imagesIdStr) && int.TryParse(imagesIdStr, out var imagesTourId))
            {
                await AdminListTourImagesAsync(chatId, imagesTourId, ct);
                return;
            }
        }
        if (data.StartsWith("a:img:del:") && data.Length > 10)
        {
            var parts = data["a:img:del:".Length..].Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out var imgId) && int.TryParse(parts[1], out var imgTourId))
            {
                var img = await _db.TourImages.FindAsync(new object[] { imgId }, ct);
                if (img != null)
                {
                    // Удаляем файл
                    try
                    {
                        var fullPath = _images.GetFullPath(img.FilePath);
                        if (System.IO.File.Exists(fullPath))
                            System.IO.File.Delete(fullPath);
                    }
                    catch { /* игнорируем ошибки удаления файла */ }
                    
                    _db.TourImages.Remove(img);
                    var tour = await _db.Tours.FindAsync(new object[] { imgTourId }, ct);
                    if (tour != null)
                        tour.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                    await _client.SendMessage(chatId, "✅ Картинка удалена.", cancellationToken: ct);
                    await AdminListTourImagesAsync(chatId, imgTourId, ct);
                }
            }
            return;
        }
        if (data.StartsWith("a:img:add:"))
        {
            var addImgIdStr = data["a:img:add:".Length..];
            if (!string.IsNullOrEmpty(addImgIdStr) && int.TryParse(addImgIdStr, out var addImgTourId))
            {
                _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitTourImage, TourId = addImgTourId });
                var buttons = new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "a:img:ready:" + addImgTourId) },
                    new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:img:cancel:" + addImgTourId) }
                };
                await _client.SendMessage(chatId, 
                    "📷 Отправьте фото для добавления в галерею тура.\n\n" +
                    "Можно отправить несколько фото сразу (выберите несколько фото в галерее и отправьте альбомом) или по одному.\n\n" +
                    "Нажмите «Готово» для завершения или «Отмена» для отмены:", 
                    replyMarkup: new InlineKeyboardMarkup(buttons),
                    cancellationToken: ct);
                return;
            }
        }
        if (data.StartsWith("a:img:ready:"))
        {
            var readyIdStr = data["a:img:ready:".Length..];
            if (!string.IsNullOrEmpty(readyIdStr) && int.TryParse(readyIdStr, out var readyTourId))
            {
                _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitTourField, TourId = readyTourId, TourField = null });
                await _client.SendMessage(chatId, "✅ Загрузка картинок завершена.", cancellationToken: ct);
                await ShowTourEditButtonsAsync(chatId, readyTourId, "Выберите поле для изменения:", ct);
                return;
            }
        }
        if (data.StartsWith("a:img:cancel:"))
        {
            var cancelIdStr = data["a:img:cancel:".Length..];
            if (!string.IsNullOrEmpty(cancelIdStr) && int.TryParse(cancelIdStr, out var cancelTourId))
            {
                _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitTourField, TourId = cancelTourId, TourField = null });
                await _client.SendMessage(chatId, "❌ Загрузка картинок отменена.", cancellationToken: ct);
                await ShowTourEditButtonsAsync(chatId, cancelTourId, "Выберите поле для изменения:", ct);
                return;
            }
        }
        if (data.StartsWith("a:tour:field:cancel:"))
        {
            var cancelFieldIdStr = data["a:tour:field:cancel:".Length..];
            if (!string.IsNullOrEmpty(cancelFieldIdStr) && int.TryParse(cancelFieldIdStr, out var cancelFieldTourId))
            {
                _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitTourField, TourId = cancelFieldTourId, TourField = null });
                await _client.SendMessage(chatId, "❌ Редактирование поля отменено.", cancellationToken: ct);
                await ShowTourEditButtonsAsync(chatId, cancelFieldTourId, "Выберите поле для изменения:", ct);
                return;
            }
        }
        if (data.StartsWith("a:tour:date:add:") && data.Length > 16 && int.TryParse(data.AsSpan(16), out var addDateTourId))
        {
            _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitTourDate, TourId = addDateTourId });
            var cancelDateButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:tour:date:cancel:" + addDateTourId) };
            await _client.SendMessage(chatId, "Введите дату выезда (ДД.ММ.ГГГГ, например 15.03.2026):", 
                replyMarkup: new InlineKeyboardMarkup(cancelDateButton), cancellationToken: ct);
            return;
        }
        if (data.StartsWith("a:tour:date:cancel:"))
        {
            var cancelDateIdStr = data["a:tour:date:cancel:".Length..];
            if (!string.IsNullOrEmpty(cancelDateIdStr) && int.TryParse(cancelDateIdStr, out var cancelDateTourId))
            {
                _state.Set<AdminConversationState>(chatId, null);
                await _client.SendMessage(chatId, "❌ Добавление даты отменено.", cancellationToken: ct);
                await ShowTourEditButtonsAsync(chatId, cancelDateTourId, "Редактирование тура:", ct);
                return;
            }
        }
        if (data.StartsWith("a:tour:date:del:") && data.Length > 16 && int.TryParse(data.AsSpan(16), out var delDateId))
        {
            var td = await _db.TourDates.FindAsync(new object[] { delDateId }, ct);
            if (td != null)
            {
                var tid = td.TourId;
                _db.TourDates.Remove(td);
                await _db.SaveChangesAsync(ct);
                await _client.SendMessage(chatId, "Дата удалена.", cancellationToken: ct);
                await AdminListTourDatesAsync(chatId, tid, ct);
            }
            return;
        }
        if (data.StartsWith("a:tour:del:") && data.Length > 11 && int.TryParse(data.AsSpan(11), out var tourIdDel))
        {
            var tour = await _db.Tours.FindAsync(new object[] { tourIdDel }, ct);
            if (tour != null)
            {
                _db.Tours.Remove(tour);
                await _db.SaveChangesAsync(ct);
                await _client.SendMessage(chatId, "Тур удалён.", cancellationToken: ct);
            }
            await AdminListToursAsync(chatId, ct);
            return;
        }

        if (data == "a:pages")
        {
            var row1 = new[] { InlineKeyboardButton.WithCallbackData("Текст «О нас»", "a:page:about"), InlineKeyboardButton.WithCallbackData("Контакты и связь", "a:page:contact") };
            await _client.SendMessage(chatId, "Редактирование текстов, которые видят пользователи:", replyMarkup: new InlineKeyboardMarkup(new[] { row1, new[] { InlineKeyboardButton.WithCallbackData("← В меню", "a:menu") } }), cancellationToken: ct);
            return;
        }
        if (data == "a:page:about" || data == "a:page:contact")
        {
            var key = data == "a:page:about" ? "about_us" : "contact_us";
            _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitPageContent, TargetKey = key });
            var cancelPageButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:page:cancel") };
            await _client.SendMessage(chatId, "Отправьте новый текст для этой страницы:", 
                replyMarkup: new InlineKeyboardMarkup(cancelPageButton), cancellationToken: ct);
            return;
        }

        if (data == "a:bookings")
        {
            var allBookings = await _db.Bookings.Include(b => b.Tour).Include(b => b.TourDate).ToListAsync(ct);
            if (allBookings.Count == 0)
            {
                await _client.SendMessage(chatId, "Заявок на бронирование пока нет.", cancellationToken: ct);
                await ShowAdminMenuAsync(chatId, ct);
                return;
            }
            var newCount = allBookings.Count(b => b.Status == "new" || b.Status == "pending");
            var confirmedCount = allBookings.Count(b => b.Status == "confirmed");
            var toursWithBookings = allBookings.GroupBy(b => b.TourId).Select(g => new { TourId = g.Key, Tour = g.First().Tour, Count = g.Count() }).OrderByDescending(x => x.Count).ToList();
            var rows = new List<InlineKeyboardButton[]>();
            if (newCount > 0)
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData($"🆕 Новые заявки ({newCount})", "a:bookings:new") });
            if (confirmedCount > 0)
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData($"✅ Подтверждённые заявки ({confirmedCount})", "a:bookings:confirmed") });
            foreach (var t in toursWithBookings)
            {
                var tourName = t.Tour?.Name ?? "Тур #" + t.TourId;
                var tourBookingsCount = allBookings.Count(b => b.TourId == t.TourId);
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData(tourName + $" ({tourBookingsCount})", "a:bookings:tour:" + t.TourId) });
            }
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← В меню", "a:menu") });
            await _client.SendMessage(chatId, "Заявки на бронирование. Выберите раздел или тур:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
            return;
        }
        if (data == "a:bookings:new")
        {
            await ShowNewBookingsAsync(chatId, ct);
            return;
        }
        if (data == "a:bookings:confirmed")
        {
            var confirmedBookings = await _db.Bookings.Include(b => b.Tour).Where(b => b.Status == "confirmed").ToListAsync(ct);
            if (confirmedBookings.Count == 0)
            {
                await _client.SendMessage(chatId, "Подтверждённых заявок нет.", cancellationToken: ct);
                await ShowAdminMenuAsync(chatId, ct);
                return;
            }
            var toursWithConfirmed = confirmedBookings.GroupBy(b => b.TourId).Select(g => new { TourId = g.Key, Tour = g.First().Tour, Count = g.Count() }).OrderByDescending(x => x.Count).ToList();
            var rows = toursWithConfirmed.Select(t => new[] { InlineKeyboardButton.WithCallbackData((t.Tour?.Name ?? "Тур #" + t.TourId) + $" ({t.Count})", "a:bookings:confirmed:tour:" + t.TourId) }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← К списку", "a:bookings") });
            await _client.SendMessage(chatId, "Подтверждённые заявки. Выберите тур:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
            return;
        }
        if (data.StartsWith("a:bookings:confirmed:tour:") && data.Length > 26 && int.TryParse(data.AsSpan(26), out var confirmedTourId))
        {
            await ShowConfirmedTourBookingsAsync(chatId, confirmedTourId, ct);
            return;
        }
        if (data.StartsWith("a:bookings:tour:") && data.Length > 16 && int.TryParse(data.AsSpan(16), out var tourBookingsId))
        {
            await ShowTourBookingsAsync(chatId, tourBookingsId, ct);
            return;
        }
        if (data.StartsWith("a:bookings:closeall:") && data.Length > 20 && int.TryParse(data.AsSpan(20), out var closeAllTourId))
        {
            var bookings = await _db.Bookings.Where(b => b.TourId == closeAllTourId).ToListAsync(ct);
            var count = bookings.Count;
            if (count > 0)
            {
                foreach (var b in bookings)
                {
                    if (b.TourDateId.HasValue)
                    {
                        var td = await _db.TourDates.FindAsync(new object[] { b.TourDateId.Value }, ct);
                        if (td != null)
                        {
                            td.PlacesBooked -= b.PlacesCount;
                            if (td.PlacesBooked < 0) td.PlacesBooked = 0;
                        }
                    }
                }
                _db.Bookings.RemoveRange(bookings);
                await _db.SaveChangesAsync(ct);
                await _client.SendMessage(chatId, $"Закрыто и удалено заявок: {count}.", cancellationToken: ct);
            }
            var allBookings = await _db.Bookings.Include(b => b.Tour).ToListAsync(ct);
            if (allBookings.Count == 0)
                await ShowAdminMenuAsync(chatId, ct);
            else
            {
                var newCount = allBookings.Count(b => b.Status == "new" || b.Status == "pending");
                var confirmedCount = allBookings.Count(b => b.Status == "confirmed");
                var toursWithBookings = allBookings.GroupBy(b => b.TourId).Select(g => new { TourId = g.Key, Tour = g.First().Tour, Count = g.Count() }).OrderByDescending(x => x.Count).ToList();
                var rows = new List<InlineKeyboardButton[]>();
                if (newCount > 0)
                    rows.Add(new[] { InlineKeyboardButton.WithCallbackData($"🆕 Новые заявки ({newCount})", "a:bookings:new") });
                if (confirmedCount > 0)
                    rows.Add(new[] { InlineKeyboardButton.WithCallbackData($"✅ Подтверждённые заявки ({confirmedCount})", "a:bookings:confirmed") });
                foreach (var t in toursWithBookings)
                {
                    var tourName = t.Tour?.Name ?? "Тур #" + t.TourId;
                    var tourBookingsCount = allBookings.Count(b => b.TourId == t.TourId);
                    rows.Add(new[] { InlineKeyboardButton.WithCallbackData(tourName + $" ({tourBookingsCount})", "a:bookings:tour:" + t.TourId) });
                }
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← В меню", "a:menu") });
                await _client.SendMessage(chatId, "Заявки на бронирование. Выберите раздел или тур:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
            }
            return;
        }
        if (data.StartsWith("a:book:confirm:"))
        {
            var parts = data["a:book:confirm:".Length..].Split(':');
            if (parts.Length > 0 && int.TryParse(parts[0], out var confirmBookId))
            {
                var b = await _db.Bookings.Include(x => x.Tour).FirstOrDefaultAsync(x => x.Id == confirmBookId, ct);
                if (b != null)
                {
                    b.Status = "confirmed";
                    await _db.SaveChangesAsync(ct);
                    try
                    {
                        var tourName = b.Tour?.Name ?? "тур";
                        await _client.SendMessage(b.TelegramChatId,
                            $"✅ Ваша заявка на «{tourName}» подтверждена.\n\nС вами скоро свяжется турагент.",
                            cancellationToken: ct);
                    }
                    catch { /* игнорируем ошибки отправки пользователю */ }
                    await _client.SendMessage(chatId, "Заявка отмечена как «Подтверждена». Пользователю отправлено уведомление.", cancellationToken: ct);
                    if (parts.Length > 1 && parts[1] == "new")
                        await ShowNewBookingsAsync(chatId, ct);
                    else if (parts.Length > 2 && parts[1] == "tour" && int.TryParse(parts[2], out var tourId))
                        await ShowTourBookingsAsync(chatId, tourId, ct);
                    else
                        await ShowAdminMenuAsync(chatId, ct);
                }
                else
                    await ShowAdminMenuAsync(chatId, ct);
            }
            return;
        }
        if (data.StartsWith("a:book:close:"))
        {
            var parts = data["a:book:close:".Length..].Split(':');
            if (parts.Length > 0 && int.TryParse(parts[0], out var closeBookId))
            {
                var b = await _db.Bookings.FindAsync(new object[] { closeBookId }, ct);
                if (b != null)
                {
                    var tourId = b.TourId;
                    if (b.TourDateId.HasValue)
                    {
                        var td = await _db.TourDates.FindAsync(new object[] { b.TourDateId.Value }, ct);
                        if (td != null)
                        {
                            td.PlacesBooked -= b.PlacesCount;
                            if (td.PlacesBooked < 0) td.PlacesBooked = 0;
                        }
                    }
                    _db.Bookings.Remove(b);
                    await _db.SaveChangesAsync(ct);
                    await _client.SendMessage(chatId, "Заявка закрыта и удалена из списка.", cancellationToken: ct);
                    if (parts.Length > 1 && parts[1] == "new")
                        await ShowNewBookingsAsync(chatId, ct);
                    else if (parts.Length > 2 && parts[1] == "tour" && int.TryParse(parts[2], out var tId))
                    {
                        if (parts.Length > 3 && parts[3] == "confirmed")
                            await ShowConfirmedTourBookingsAsync(chatId, tId, ct);
                        else
                            await ShowTourBookingsAsync(chatId, tId, ct);
                    }
                    else
                        await ShowAdminMenuAsync(chatId, ct);
                }
                else
                    await ShowAdminMenuAsync(chatId, ct);
            }
            return;
        }
        if (data == "a:password")
        {
            _state.Set(chatId, new AdminConversationState { Step = AdminStep.WaitAdminOldPassword });
            var cancelPasswordButton = new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "a:password:cancel") };
            await _client.SendMessage(chatId, "Смена пароля. Введите текущий пароль:", 
                replyMarkup: new InlineKeyboardMarkup(cancelPasswordButton), cancellationToken: ct);
            return;
        }

        if (data == "a:broadcast")
        {
            var tours = await _db.Tours.Include(t => t.TourDates).Take(20).ToListAsync(ct);
            var rows = tours.Select(t => new[] { InlineKeyboardButton.WithCallbackData(t.Name, "a:bc:send:" + t.Id) }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← В меню", "a:menu") });
            await _client.SendMessage(chatId, "Выберите тур — его описание будет разослано всем, кто когда-либо запускал бота:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
            return;
        }
        if (data.StartsWith("a:bc:send:") && int.TryParse(data.AsSpan(10), out var bcTourId))
        {
            var tour = await _db.Tours.Include(t => t.TourDates).Include(t => t.TourImages.OrderBy(i => i.SortOrder)).FirstOrDefaultAsync(t => t.Id == bcTourId, ct);
            if (tour == null) return;
            var users = await _db.BotUsers.ToListAsync(ct);
            var sent = 0;
            
            // Используем первую картинку из галереи вместо баннера
            var firstImage = tour.TourImages.FirstOrDefault();
            var imagePath = firstImage != null ? _images.GetFullPath(firstImage.FilePath) : null;
            
            foreach (var u in users)
            {
                try
                {
                    if (imagePath != null && System.IO.File.Exists(imagePath))
                    {
                        await using var stream = System.IO.File.OpenRead(imagePath);
                        await _client.SendPhoto(u.TelegramChatId, InputFile.FromStream(stream, Path.GetFileName(imagePath)), 
                            caption: $"{tour.Name}\n{tour.Cost}\nЗабронировать: нажмите /start", cancellationToken: ct);
                    }
                    else
                        await _client.SendMessage(u.TelegramChatId, $"{tour.Name}\n{tour.Cost}\n/start — забронировать", cancellationToken: ct);
                    sent++;
                }
                catch { /* skip */ }
            }
            await _client.SendMessage(chatId, $"Готово. Сообщение отправлено {sent} из {users.Count} пользователям.", cancellationToken: ct);
            await ShowAdminMenuAsync(chatId, ct);
            return;
        }

        await ShowAdminMenuAsync(chatId, ct);
    }

    private async Task AdminListDirectionsAsync(long chatId, CancellationToken ct)
    {
        var dirs = await _db.TourDirections.OrderBy(d => d.SortOrder).ToListAsync(ct);
        var rows = dirs.Select(d => new[]
        {
            InlineKeyboardButton.WithCallbackData("✏ " + d.Name, "a:dir:edit:" + d.Id),
            InlineKeyboardButton.WithCallbackData("🗑", "a:dir:del:" + d.Id)
        }).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("+ Добавить направление", "a:dir:add") });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← В меню", "a:menu") });
        await _client.SendMessage(chatId, "Направления туров (категории, по которым сортируются туры):", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    private async Task AdminListToursAsync(long chatId, CancellationToken ct)
    {
        var tours = await _db.Tours.Include(t => t.TourDirection).OrderBy(t => t.TourDirection!.SortOrder).ThenBy(t => t.Name).Take(30).ToListAsync(ct);
        var rows = tours.Select(t => new[]
        {
            InlineKeyboardButton.WithCallbackData(t.Name.Length > 25 ? t.Name[..22] + "…" : t.Name, "a:tour:edit:" + t.Id),
            InlineKeyboardButton.WithCallbackData("🗑", "a:tour:del:" + t.Id)
        }).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("+ Добавить тур", "a:tour:add") });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← В меню", "a:menu") });
        await _client.SendMessage(chatId, "Список туров. Нажмите на тур, чтобы изменить описание, даты и цены:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    private async Task AdminListTourImagesAsync(long chatId, int tourId, CancellationToken ct)
    {
        try
        {
            var tour = await _db.Tours.Include(t => t.TourImages.OrderBy(i => i.SortOrder)).FirstOrDefaultAsync(t => t.Id == tourId, ct);
            if (tour == null)
            {
                await _client.SendMessage(chatId, "Тур не найден.", cancellationToken: ct);
                await ShowAdminMenuAsync(chatId, ct);
                return;
            }

            await _client.SendMessage(chatId, $"Картинки тура «{tour.Name}»:\n\nГалерея: {tour.TourImages.Count} картинок", cancellationToken: ct);

            // Показываем картинки галереи
            if (tour.TourImages.Count > 0)
            {
                foreach (var img in tour.TourImages)
                {
                    var imgPath = _images.GetFullPath(img.FilePath);
                    var buttons = new List<InlineKeyboardButton>();
                    buttons.Add(InlineKeyboardButton.WithCallbackData("🗑 Удалить", "a:img:del:" + img.Id + ":" + tourId));
                    
                    if (System.IO.File.Exists(imgPath))
                    {
                        await using var imgStream = System.IO.File.OpenRead(imgPath);
                        await _client.SendPhoto(chatId, InputFile.FromStream(imgStream, Path.GetFileName(imgPath)), 
                            caption: img.Caption ?? $"Картинка #{img.SortOrder + 1}", 
                            replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
                    }
                    else
                    {
                        // Если файл не существует, показываем текстовое сообщение с кнопками управления
                        var caption = img.Caption ?? $"Картинка #{img.SortOrder + 1}";
                        await _client.SendMessage(chatId, $"⚠️ Файл картинки не найден: {img.FilePath}\n{caption}", 
                            replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
                    }
                }
            }

            var rows = new List<InlineKeyboardButton[]>();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("+ Добавить картинку", "a:img:add:" + tourId) });
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← К полям тура", "a:tour:edit:" + tourId) });
            await _client.SendMessage(chatId, "Управление картинками:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await _client.SendMessage(chatId, $"Ошибка при загрузке картинок: {ex.Message}", cancellationToken: ct);
            await ShowAdminMenuAsync(chatId, ct);
        }
    }

    private async Task ShowNewBookingsAsync(long chatId, CancellationToken ct)
    {
        var newBookings = await _db.Bookings.Include(b => b.Tour).Include(b => b.TourDate).Where(b => b.Status == "new" || b.Status == "pending").OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
        if (newBookings.Count == 0)
        {
            await _client.SendMessage(chatId, "Новых заявок нет.", cancellationToken: ct);
            await ShowAdminMenuAsync(chatId, ct);
            return;
        }
        await _client.SendMessage(chatId, $"🆕 Новые заявки ({newBookings.Count}). Под каждой заявкой — кнопки «Подтвердить» и «Закрыть»:", cancellationToken: ct);
        foreach (var b in newBookings)
        {
            var statusText = GetBookingStatusDisplay(b.Status);
            var text = $"Заявка №{b.Id}\n{b.LastName} {b.FirstName}\nТур: {b.Tour?.Name}\nДата: {(b.TourDate != null ? b.TourDate.Date.ToString("dd.MM.yyyy") : "не указана")}\nТелефон: {b.Phone}\nКоличество мест: {b.PlacesCount}\nСтатус: {statusText}";
            if (b.TourDate != null)
            {
                var free = b.TourDate.PlacesTotal - b.TourDate.PlacesBooked;
                if (free < 0) free = 0;
                text += $"\nМест на эту дату: всего {b.TourDate.PlacesTotal}, занято {b.TourDate.PlacesBooked}, свободно {free}";
            }
            if (!string.IsNullOrEmpty(b.Comment))
                text += $"\nКомментарий: {b.Comment}";
            var row = new List<InlineKeyboardButton>();
            row.Add(InlineKeyboardButton.WithCallbackData("Подтвердить", "a:book:confirm:" + b.Id + ":new"));
            row.Add(InlineKeyboardButton.WithCallbackData("Закрыть и удалить", "a:book:close:" + b.Id + ":new"));
            await _client.SendMessage(chatId, text, replyMarkup: new InlineKeyboardMarkup(row), cancellationToken: ct);
        }
        var backRow = new[] { InlineKeyboardButton.WithCallbackData("← К списку туров", "a:bookings") };
        await _client.SendMessage(chatId, "Новые заявки:", replyMarkup: new InlineKeyboardMarkup(backRow), cancellationToken: ct);
    }

    private async Task ShowConfirmedTourBookingsAsync(long chatId, int tourId, CancellationToken ct)
    {
        var list = await _db.Bookings.Include(b => b.Tour).Include(b => b.TourDate).Where(b => b.TourId == tourId && b.Status == "confirmed").OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
        if (list.Count == 0)
        {
            await _client.SendMessage(chatId, "Подтверждённых заявок по этому туру нет.", cancellationToken: ct);
            await ShowAdminMenuAsync(chatId, ct);
            return;
        }
        var tour = await _db.Tours.FindAsync(new object[] { tourId }, ct);
        await _client.SendMessage(chatId, $"✅ Подтверждённые заявки по туру «{tour?.Name ?? "Тур #" + tourId}» ({list.Count}):", cancellationToken: ct);
        foreach (var b in list)
        {
            var text = $"Заявка №{b.Id}\n{b.LastName} {b.FirstName}\nДата: {(b.TourDate != null ? b.TourDate.Date.ToString("dd.MM.yyyy") : "не указана")}\nТелефон: {b.Phone}\nКоличество мест: {b.PlacesCount}\nСтатус: Подтверждена";
            if (b.TourDate != null)
            {
                var free = b.TourDate.PlacesTotal - b.TourDate.PlacesBooked;
                if (free < 0) free = 0;
                text += $"\nМест на эту дату: всего {b.TourDate.PlacesTotal}, занято {b.TourDate.PlacesBooked}, свободно {free}";
            }
            if (!string.IsNullOrEmpty(b.Comment))
                text += $"\nКомментарий: {b.Comment}";
            var row = new[] { InlineKeyboardButton.WithCallbackData("Закрыть и удалить", "a:book:close:" + b.Id + ":tour:" + tourId + ":confirmed") };
            await _client.SendMessage(chatId, text, replyMarkup: new InlineKeyboardMarkup(row), cancellationToken: ct);
        }
        var backRow = new[] { InlineKeyboardButton.WithCallbackData("← К подтверждённым", "a:bookings:confirmed") };
        await _client.SendMessage(chatId, "Подтверждённые заявки по туру:", replyMarkup: new InlineKeyboardMarkup(backRow), cancellationToken: ct);
    }

    private async Task ShowTourBookingsAsync(long chatId, int tourId, CancellationToken ct)
    {
        var tourBookings = await _db.Bookings.Include(b => b.Tour).Include(b => b.TourDate).Where(b => b.TourId == tourId).OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
        if (tourBookings.Count == 0)
        {
            await _client.SendMessage(chatId, "Заявок по этому туру нет.", cancellationToken: ct);
            await ShowAdminMenuAsync(chatId, ct);
            return;
        }
        var tour = await _db.Tours.FindAsync(new object[] { tourId }, ct);
        await _client.SendMessage(chatId, $"Заявки по туру «{tour?.Name ?? "Тур #" + tourId}» ({tourBookings.Count}). Под каждой заявкой — кнопки действий:", cancellationToken: ct);
        foreach (var b in tourBookings)
        {
            var statusText = GetBookingStatusDisplay(b.Status);
            var text = $"Заявка №{b.Id}\n{b.LastName} {b.FirstName}\nДата: {(b.TourDate != null ? b.TourDate.Date.ToString("dd.MM.yyyy") : "не указана")}\nТелефон: {b.Phone}\nКоличество мест: {b.PlacesCount}\nСтатус: {statusText}";
            if (b.TourDate != null)
            {
                var free = b.TourDate.PlacesTotal - b.TourDate.PlacesBooked;
                if (free < 0) free = 0;
                text += $"\nМест на эту дату: всего {b.TourDate.PlacesTotal}, занято {b.TourDate.PlacesBooked}, свободно {free}";
            }
            if (!string.IsNullOrEmpty(b.Comment))
                text += $"\nКомментарий: {b.Comment}";
            var row = new List<InlineKeyboardButton>();
            if (b.Status != "confirmed")
                row.Add(InlineKeyboardButton.WithCallbackData("Подтвердить", "a:book:confirm:" + b.Id + ":tour:" + tourId));
            row.Add(InlineKeyboardButton.WithCallbackData("Закрыть и удалить", "a:book:close:" + b.Id + ":tour:" + tourId));
            await _client.SendMessage(chatId, text, replyMarkup: new InlineKeyboardMarkup(row), cancellationToken: ct);
        }
        var closeAllRow = new[] { InlineKeyboardButton.WithCallbackData("🗑 Закрыть все заявки по этому туру", "a:bookings:closeall:" + tourId) };
        var backToToursRow = new[] { InlineKeyboardButton.WithCallbackData("← К списку туров", "a:bookings") };
        await _client.SendMessage(chatId, "Заявки по туру:", replyMarkup: new InlineKeyboardMarkup(new[] { closeAllRow, backToToursRow }), cancellationToken: ct);
    }

    private async Task AdminListTourDatesAsync(long chatId, int tourId, CancellationToken ct)
    {
        var dates = await _db.TourDates.Where(d => d.TourId == tourId).OrderBy(d => d.Date).ToListAsync(ct);
        var rows = dates.Select(d => new[] { InlineKeyboardButton.WithCallbackData(d.Date.ToString("dd.MM.yyyy") + " — " + d.PlacesTotal + " мест 🗑", "a:tour:date:del:" + d.Id) }).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("+ Добавить дату", "a:tour:date:add:" + tourId) });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("← К полям тура", "a:tour:edit:" + tourId) });
        var msg = dates.Count > 0 ? "Даты тура (появятся в календаре, нажмите чтобы удалить):" : "Дат пока нет. Добавьте дату — тур появится в календаре.";
        await _client.SendMessage(chatId, msg, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    private class AdminConversationState
    {
        public AdminStep Step { get; set; }
        public string? TargetKey { get; set; }
        public int? DirectionId { get; set; }
        public int? TourId { get; set; }
        public string? TourField { get; set; }
        public int SortOrder { get; set; }
        public bool IsEdit { get; set; }
        public DateTime? TempDate { get; set; }
        public string? TempPassword { get; set; }
    }

    private enum AdminStep
    {
        None,
        WaitPageContent,
        WaitDirectionName,
        WaitTourField,
        WaitTourDate,
        WaitTourDatePlaces,
        WaitTourImage,
        WaitAdminOldPassword,
        WaitAdminNewPassword
    }

    #endregion
}

