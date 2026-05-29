namespace TravelBot.Bot;

public sealed class UserSessionStore
{
    private readonly Dictionary<long, UserSession> _sessions = new();

    public UserSession GetOrCreate(long chatId)
    {
        if (!_sessions.TryGetValue(chatId, out var session))
        {
            session = new UserSession();
            _sessions[chatId] = session;
        }

        return session;
    }

    public void Reset(long chatId)
    {
        _sessions[chatId] = new UserSession();
    }
}
