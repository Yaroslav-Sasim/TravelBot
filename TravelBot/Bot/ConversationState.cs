namespace TravelBot.Bot;

public enum BookingStep
{
    None,
    ChoosingDate,
    FirstName,
    LastName,
    Phone,
    PlacesCount,
    Comment,
    Done
}

public class BookingStateData
{
    public int TourId { get; set; }
    public int? TourDateId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public int PlacesCount { get; set; } = 1;
    public string? Comment { get; set; }
    public BookingStep Step { get; set; }
}

public class ConversationState
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, object?> _userState = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, bool> _adminSessions = new();

    public T? Get<T>(long chatId) where T : class
    {
        return _userState.TryGetValue(chatId, out var v) ? v as T : null;
    }

    public void Set<T>(long chatId, T? value) where T : class
    {
        if (value == null)
            _userState.TryRemove(chatId, out _);
        else
            _userState[chatId] = value;
    }

    public bool IsAdmin(long chatId) => _adminSessions.TryGetValue(chatId, out var v) && v;

    public void SetAdmin(long chatId, bool isAdmin)
    {
        if (isAdmin)
            _adminSessions[chatId] = true;
        else
            _adminSessions.TryRemove(chatId, out _);
    }
}

