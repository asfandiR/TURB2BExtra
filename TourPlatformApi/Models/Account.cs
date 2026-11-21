namespace TourPlatformApi.Models;

public sealed class Account
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public AccountType AccountType { get; set; }
    public SubscriptionPlan SubscriptionPlan { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ExpiresAt { get; set; }

    public ICollection<User> Users { get; set; } = [];
}
