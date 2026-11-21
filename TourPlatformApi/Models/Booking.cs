namespace TourPlatformApi.Models;

public sealed class Booking
{
    public int Id { get; set; }
    public int AgencyAccountId { get; set; }
    public int? IntermediaryAccountId { get; set; }
    public int GuideUserId { get; set; }
    public int? TripId { get; set; }
    public int? HotelId { get; set; }
    public string? CustomPickupPoint { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public decimal TicketPrice { get; set; }
    public decimal GuideCommission { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RestAmount { get; set; }
    public Currency Currency { get; set; }
    public BookingStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Account AgencyAccount { get; set; } = null!;
    public Account? IntermediaryAccount { get; set; }
    public User Guide { get; set; } = null!;
    public Trip? Trip { get; set; }
    public Hotel? Hotel { get; set; }
}
