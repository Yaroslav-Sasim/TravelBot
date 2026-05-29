using System.Text;

namespace TravelBot.Services;

public sealed class AgencyService
{
    private readonly IConfiguration _config;

    public AgencyService(IConfiguration config)
    {
        _config = config;
    }

    public string GetAboutText() =>
        _config["Agency:About"]
        ?? "TravelBot — турагентство, которое помогает выбрать и забронировать путешествие.";

    public string GetContactText()
    {
        var phones = _config.GetSection("Agency:Phones").Get<string[]>() ?? ["+7 (900) 000-00-00"];
        var email = _config["Agency:Email"] ?? "info@travelbot.ru";
        var socials = _config.GetSection("Agency:Socials").Get<string[]>() ?? ["Telegram: @travelbot"];

        return new StringBuilder()
            .AppendLine("📞 Связь с нами")
            .AppendLine()
            .AppendLine("Телефоны:")
            .AppendLine(string.Join('\n', phones))
            .AppendLine()
            .AppendLine($"Почта: {email}")
            .AppendLine()
            .AppendLine("Соцсети:")
            .AppendLine(string.Join('\n', socials))
            .ToString();
    }
}
