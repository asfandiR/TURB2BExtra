using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TourPlatformApi.Data;
using TourPlatformApi.DTOs;

namespace TourPlatformApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await db.Users
            .Include(x => x.Account)
            .SingleOrDefaultAsync(x => x.Email.ToLower() == request.Email.ToLower());

        if (user is null || !user.IsActive || !user.Account.IsActive ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password, or account is inactive." });
        }

        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("AccountId", user.AccountId.ToString()),
            new Claim("AccountType", user.Account.AccountType.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        return Ok(new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt));
    }
}
