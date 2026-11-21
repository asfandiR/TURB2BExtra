using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TourPlatformApi.Domain;

namespace TourPlatformApi.Auth;

public interface IJwtService
{
    LoginResponse CreateToken(User user);
}

public sealed class JwtService(IConfiguration configuration) : IJwtService
{
    public LoginResponse CreateToken(User user)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "TURB2BExtra";
        var audience = configuration["Jwt:Audience"] ?? "TURB2BExtra";
        var signingKey = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is not configured.");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Login),
            new Claim("account_id", user.AccountId.ToString()),
            new Claim("account_type", user.Account.AccountType.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            user.AccountId,
            user.Account.AccountType,
            user.Role);
    }
}
