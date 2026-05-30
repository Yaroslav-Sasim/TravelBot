namespace TravelBot.Models;

public class PageContent
{
    public int Id { get; set; }
    /// <summary>Ключ: about_us, contact_us.</summary>
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

