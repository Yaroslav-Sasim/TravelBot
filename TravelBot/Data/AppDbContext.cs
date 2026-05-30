using Microsoft.EntityFrameworkCore;
using TravelBot.Models;

namespace TravelBot.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<TourDirection> TourDirections => Set<TourDirection>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourDate> TourDates => Set<TourDate>();
    public DbSet<TourImage> TourImages => Set<TourImage>();
    public DbSet<TourKeyword> TourKeywords => Set<TourKeyword>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<PageContent> PageContents => Set<PageContent>();
    public DbSet<Broadcast> Broadcasts => Set<Broadcast>();
    public DbSet<BotUser> BotUsers => Set<BotUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tour>().HasOne(t => t.TourDirection).WithMany(d => d.Tours).HasForeignKey(t => t.TourDirectionId);
        modelBuilder.Entity<TourDate>().HasOne(t => t.Tour).WithMany(t => t.TourDates).HasForeignKey(t => t.TourId);
        modelBuilder.Entity<TourImage>().HasOne(t => t.Tour).WithMany(t => t.TourImages).HasForeignKey(t => t.TourId);
        modelBuilder.Entity<TourKeyword>().HasOne(t => t.Tour).WithMany(t => t.TourKeywords).HasForeignKey(t => t.TourId);
        modelBuilder.Entity<Booking>().HasOne(b => b.Tour).WithMany(t => t.Bookings).HasForeignKey(b => b.TourId);
        modelBuilder.Entity<Booking>().HasOne(b => b.TourDate).WithMany(d => d.Bookings).HasForeignKey(b => b.TourDateId).IsRequired(false);
        modelBuilder.Entity<Broadcast>().HasOne(b => b.Tour).WithMany().HasForeignKey(b => b.TourId);
        modelBuilder.Entity<BotUser>().HasIndex(b => b.TelegramUserId).IsUnique();
    }
}
