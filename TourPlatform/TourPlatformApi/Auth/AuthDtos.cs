using TourPlatformApi.Domain;

namespace TourPlatformApi.Auth;

/// <summary>Credentials used to sign in to an account.</summary>
public sealed record LoginRequest
{
    public required string Login { get; init; }
    public required string Password { get; init; }
}

/// <summary>JWT access token and account identity.</summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid AccountId,
    AccountType AccountType,
    UserRole Role);
