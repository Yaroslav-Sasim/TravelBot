namespace TravelBot.Models;

public class Tour
{
    public int Id { get; set; }
    public int TourDirectionId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Путь к файлу баннера (относительно папки изображений).</summary>
    public string? BannerFilePath { get; set; }
    public string? Cost { get; set; }
    /// <summary>Из каких городов выезд (текст).</summary>
    public string? DepartureCities { get; set; }
    public string? Description { get; set; }
    /// <summary>Текстовая программа по дням (если не в картинках).</summary>
    public string? ProgramText { get; set; }
    public string? Included { get; set; }
    public string? ExtraPayments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public TourDirection TourDirection { get; set; } = null!;
    public ICollection<TourDate> TourDates { get; set; } = new List<TourDate>();
    public ICollection<TourImage> TourImages { get; set; } = new List<TourImage>();
    public ICollection<TourKeyword> TourKeywords { get; set; } = new List<TourKeyword>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

