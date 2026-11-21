namespace TourPlatformApi.Models;

public sealed class User
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    public Account Account { get; set; } = null!;
}
