using Microsoft.EntityFrameworkCore;
using TourPlatformApi.Models;

namespace TourPlatformApi.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Hotel> Hotels => Set<Hotel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>().Property(x => x.AccountType).HasConversion<string>();
        modelBuilder.Entity<Account>().Property(x => x.SubscriptionPlan).HasConversion<string>();

        modelBuilder.Entity<User>()
            .Property(x => x.Role).HasConversion<string>();
        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<User>()
            .HasOne(x => x.Account)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Trip>()
            .HasOne(x => x.IntermediaryAccount)
            .WithMany()
            .HasForeignKey(x => x.IntermediaryAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Trip>()
            .HasOne(x => x.Driver)
            .WithMany()
            .HasForeignKey(x => x.DriverUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Booking>()
            .Property(x => x.Currency).HasConversion<string>();
        modelBuilder.Entity<Booking>()
            .Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<Booking>()
            .HasIndex(x => x.AgencyAccountId);
        modelBuilder.Entity<Booking>()
            .HasIndex(x => x.IntermediaryAccountId);
        modelBuilder.Entity<Booking>()
            .HasIndex(x => x.GuideUserId);
        modelBuilder.Entity<Booking>()
            .HasIndex(x => x.CreatedAt);

        modelBuilder.Entity<Booking>()
            .HasOne(x => x.AgencyAccount)
            .WithMany()
            .HasForeignKey(x => x.AgencyAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Booking>()
            .HasOne(x => x.IntermediaryAccount)
            .WithMany()
            .HasForeignKey(x => x.IntermediaryAccountId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Booking>()
            .HasOne(x => x.Guide)
            .WithMany()
            .HasForeignKey(x => x.GuideUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Booking>()
            .HasOne(x => x.Trip)
            .WithMany()
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
