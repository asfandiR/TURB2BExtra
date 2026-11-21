using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TourPlatformApi.Domain;

namespace TourPlatformApi.Data;

public sealed class TourPlatformDbContext(DbContextOptions<TourPlatformDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripExpense> TripExpenses => Set<TripExpense>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<PartnerConnection> PartnerConnections => Set<PartnerConnection>();
    public DbSet<WarehouseItem> WarehouseItems => Set<WarehouseItem>();
    public DbSet<CashRegisterSale> CashRegisterSales => Set<CashRegisterSale>();
    public DbSet<PlatformNotification> Notifications => Set<PlatformNotification>();
    public DbSet<TripStaffAssignment> TripStaffAssignments => Set<TripStaffAssignment>();
    public DbSet<CompanyExpense> CompanyExpenses => Set<CompanyExpense>();
    public DbSet<ExternalPartner> ExternalPartners => Set<ExternalPartner>();
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ConnectionHexCode).IsUnique();
            entity.Property(x => x.ConnectionHexCode).HasMaxLength(16);
            entity.Property(x => x.DefaultPriceCurrency).HasConversion<string>();
            entity.Property(x => x.LimitsJson).HasColumnType("jsonb");
            entity.Property(x => x.AccountType).HasConversion<string>();
            entity.Property(x => x.Plan).HasConversion<string>();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Login).IsUnique();
            entity.Property(x => x.Role).HasConversion<string>();
            entity.HasOne(x => x.Account).WithMany(x => x.Users).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Hotel>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Region, x.Name });
        });

        modelBuilder.Entity<Tour>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DefaultExpenseBricksJson).HasColumnType("jsonb");
            entity.Property(x => x.Currency).HasConversion<string>();
            entity.HasOne(x => x.IntermediaryAccount).WithMany().HasForeignKey(x => x.IntermediaryAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DepartureDate).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.IntermediaryAccount).WithMany().HasForeignKey(x => x.IntermediaryAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tour).WithMany().HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TripExpense>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BrickType).HasConversion<string>();
            entity.Property(x => x.Currency).HasConversion<string>();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.Trip).WithMany(x => x.Expenses).HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Currency).HasConversion<string>();
            entity.Property(x => x.CommissionAmount).HasPrecision(18, 2);
            entity.Property(x => x.CashAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidCurrency).HasConversion<string>();
            entity.Property(x => x.RemainingCurrency).HasConversion<string>();
            entity.Property(x => x.TotalSaleAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.Property(x => x.RemainingAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.AgencyAccount).WithMany().HasForeignKey(x => x.AgencyAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.GuideUser).WithMany().HasForeignKey(x => x.GuideUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Trip).WithMany(x => x.Bookings).HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Hotel).WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PartnerConnection>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.AgencyAccountId, x.IntermediaryAccountId }).IsUnique();
        });
        modelBuilder.Entity<WarehouseItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AccountId);
            entity.Property(x => x.Currency).HasConversion<string>();
            entity.Property(x => x.CostPrice).HasPrecision(18, 2);
            entity.Property(x => x.SalePrice).HasPrecision(18, 2);
        });
        modelBuilder.Entity<CashRegisterSale>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.IntermediaryAccountId);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Currency).HasConversion<string>();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
        });
        modelBuilder.Entity<PlatformNotification>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AccountId);
            entity.Property(x => x.Category).HasConversion<string>();
        });
        modelBuilder.Entity<TripStaffAssignment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TripId, x.UserId, x.Role }).IsUnique();
        });
        modelBuilder.Entity<CompanyExpense>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AccountId);
            entity.Property(x => x.Category).HasConversion<string>();
            entity.Property(x => x.Currency).HasConversion<string>();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
        });
        modelBuilder.Entity<ExternalPartner>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AccountId);
        });
        modelBuilder.Entity<CurrencyRate>(entity =>
        {
            entity.HasKey(x => x.Code);
            entity.Property(x => x.Code).HasConversion<string>();
            entity.Property(x => x.RateToUsd).HasPrecision(18, 8);
        });
    }
}
