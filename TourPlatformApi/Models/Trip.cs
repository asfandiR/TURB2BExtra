namespace TourPlatformApi.Models;

public sealed class Trip
{
    public int Id { get; set; }
    public int IntermediaryAccountId { get; set; }
    public required string TourTitle { get; set; }
    public DateOnly DepartureDate { get; set; }
    public TimeOnly DepartureTime { get; set; }
    public required string VehicleNumber { get; set; }
    public int? DriverUserId { get; set; }
    public int Capacity { get; set; }

    public Account IntermediaryAccount { get; set; } = null!;
    public User? Driver { get; set; }
}
