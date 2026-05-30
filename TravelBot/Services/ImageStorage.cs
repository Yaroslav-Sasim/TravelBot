namespace TravelBot.Services;

/// <summary>Хранение картинок в папке на диске. Пути сохраняем в БД.</summary>
public class ImageStorage
{
    private readonly string _basePath;
    private readonly string _relativeBase = "Images";

    public ImageStorage(string appBasePath)
    {
        _basePath = Path.Combine(appBasePath, _relativeBase);
        Directory.CreateDirectory(_basePath);
    }

    public string GetFullPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return relativePath;
        return Path.Combine(_basePath, relativePath);
    }

    /// <summary>Сохраняет файл из потока и возвращает относительный путь для БД.</summary>
    public async Task<string> SaveAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName) ?? "image" + Path.GetExtension(fileName);
        var subDir = Path.Combine(_basePath, DateTime.UtcNow.ToString("yyyy-MM"));
        Directory.CreateDirectory(subDir);
        var fullPath = Path.Combine(subDir, Guid.NewGuid().ToString("N")[..8] + Path.GetExtension(safeName));
        await using var fs = File.Create(fullPath);
        await stream.CopyToAsync(fs, ct);
        return Path.GetRelativePath(_basePath, fullPath);
    }

    public bool Exists(string relativePath)
    {
        var full = GetFullPath(relativePath);
        return File.Exists(full);
    }

    public string BaseDirectory => _basePath;
}

