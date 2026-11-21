namespace TourPlatformApi.Domain;

public enum AccountType
{
    Agency,
    Intermediary
}

public enum SubscriptionPlan
{
    Base,
    Medium,
    Lux,
    Unlimited
}

public enum UserRole
{
    AgencyGuide,
    AgencyManager,
    AgencyAccountant,
    AgencyOwner,
    IntermediaryDriver,
    IntermediaryDispatcher,
    IntermediaryOwner,
    IntermediaryAccountant,
    IntermediaryStaffSales
}

public enum ExpenseBrickType
{
    PerTrip,
    PerPerson
}

public enum CommissionBasis
{
    TotalCashRegister,
    OwnSales
}

public enum NotificationCategory
{
    New,
    Change,
    Important
}

public enum Currency
{
    USD,
    EUR,
    GBP,
    TRY
}

public sealed class Account
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Demo company";
    public AccountType AccountType { get; set; }
    public SubscriptionPlan Plan { get; set; }
    public string ConnectionHexCode { get; set; } = string.Empty;
    public Currency DefaultPriceCurrency { get; set; } = Currency.USD;
    public string? WhatsAppTarget { get; set; }
    public bool SeasonFullPrice { get; set; }
    public bool SeasonHalfPrice { get; set; }
    public DateTimeOffset ActiveUntil { get; set; }
    public string LimitsJson { get; set; } = "{}";
    public ICollection<User> Users { get; set; } = [];
}

public sealed class User
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public string FullName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal CommissionPercent { get; set; }
    public CommissionBasis CommissionBasis { get; set; }
}

public sealed class Hotel
{
    public Guid Id { get; set; }
    public string Region { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DefaultPickupNote { get; set; } = string.Empty;
}

public sealed class Tour
{
    public Guid Id { get; set; }
    public Guid IntermediaryAccountId { get; set; }
    public Account IntermediaryAccount { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public decimal PublicPrice { get; set; }
    public Currency Currency { get; set; }
    public string DefaultExpenseBricksJson { get; set; } = "[]";
}

public sealed class Trip
{
    public Guid Id { get; set; }
    public Guid IntermediaryAccountId { get; set; }
    public Account IntermediaryAccount { get; set; } = null!;
    public Guid TourId { get; set; }
    public Tour Tour { get; set; } = null!;
    public DateTimeOffset DepartureDate { get; set; }
    public TimeOnly DepartureTime { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public Guid? DriverUserId { get; set; }
    public User? Driver { get; set; }
    public int Capacity { get; set; }
    public ICollection<TripExpense> Expenses { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
}

public sealed class TripExpense
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public ExpenseBrickType BrickType { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public bool IsPenaltyOrExtra { get; set; }
}

public sealed class Booking
{
    public Guid Id { get; set; }
    public Guid AgencyAccountId { get; set; }
    public Account AgencyAccount { get; set; } = null!;
    public Guid IntermediaryAccountId { get; set; }
    public Guid GuideUserId { get; set; }
    public User GuideUser { get; set; } = null!;
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public Guid? HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    public string? CustomPickupPoint { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal CashAmount { get; set; }
    public Currency Currency { get; set; }
    public decimal TotalSaleAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public Currency PaidCurrency { get; set; } = Currency.USD;
    public decimal RemainingAmount { get; set; }
    public Currency RemainingCurrency { get; set; } = Currency.USD;
    public decimal AgencyLossDeductionAmount { get; set; }
    public string Status { get; set; } = "Created";
}

public sealed class PartnerConnection
{
    public Guid Id { get; set; }
    public Guid AgencyAccountId { get; set; }
    public Guid IntermediaryAccountId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WarehouseItem
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public Currency Currency { get; set; }
    public decimal StockQty { get; set; }
}

public sealed class CashRegisterSale
{
    public Guid Id { get; set; }
    public Guid IntermediaryAccountId { get; set; }
    public Guid StaffUserId { get; set; }
    public Guid? ItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PlatformNotification
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? RecipientUserId { get; set; }
    public NotificationCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TripStaffAssignment
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Driver";
    public string? VehiclePlate { get; set; }
    public string? PickupZone { get; set; }
}

public enum ExpenseCategory
{
    Recurring,
    OneTime
}

public sealed class CompanyExpense
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Department { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
}

public sealed class ExternalPartner
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public sealed class CurrencyRate
{
    public Currency Code { get; set; }
    public decimal RateToUsd { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
