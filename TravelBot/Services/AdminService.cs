using Microsoft.EntityFrameworkCore;
using TravelBot.Data;
using TravelBot.Models;

namespace TravelBot.Services;

public class AdminService
{
    private readonly AppDbContext _db;

    public AdminService(AppDbContext db) => _db = db;

    public async Task<bool> ValidatePasswordAsync(string password, CancellationToken ct = default)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(ct);
        if (admin == null)
            return false;
        var hash = HashPassword(password);
        return admin.PasswordHash == hash;
    }

    public async Task EnsureAdminExistsAsync(string defaultPassword, CancellationToken ct = default)
    {
        if (await _db.Admins.AnyAsync(ct))
            return;
        _db.Admins.Add(new Admin
        {
            PasswordHash = HashPassword(defaultPassword),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (!await ValidatePasswordAsync(currentPassword, ct))
            return false;
        var admin = await _db.Admins.FirstOrDefaultAsync(ct);
        if (admin == null)
            return false;
        admin.PasswordHash = HashPassword(newPassword);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public static string HashPassword(string password)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}

