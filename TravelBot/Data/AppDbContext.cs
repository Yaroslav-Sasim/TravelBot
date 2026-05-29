using Microsoft.EntityFrameworkCore;
using TravelBot.Data.Entities;

namespace TravelBot.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TourEntity> Tours => Set<TourEntity>();
    public DbSet<TourDateEntity> TourDates => Set<TourDateEntity>();
    public DbSet<BookingEntity> Bookings => Set<BookingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TourEntity>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Price).HasPrecision(10, 2);
            entity.Property(t => t.DepartureCities).HasColumnType("text[]");
            entity.Property(t => t.ProgramImages).HasColumnType("text[]");
        });

        modelBuilder.Entity<TourDateEntity>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.HasOne(d => d.Tour)
                .WithMany(t => t.Dates)
                .HasForeignKey(d => d.TourId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookingEntity>(entity =>
        {
            entity.HasKey(b => b.Id);
        });
    }
}
