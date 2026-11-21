namespace TourPlatformApi.Models;

public sealed class Hotel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Address { get; set; }
}
