using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourPlatformApi.Data;
using TourPlatformApi.DTOs;
using TourPlatformApi.Models;

namespace TourPlatformApi.Controllers;

[ApiController]
[Authorize]
[Route("api/driver/trips")]
public sealed class DriverTripsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DriverTripResponse>>> GetTodayAndTomorrow()
    {
        if (!TryGetScope(out var userId, out var accountId, out var accountType, out var role) ||
            accountType != AccountType.Intermediary.ToString() || role != UserRole.IntermediaryDriver.ToString())
        {
            return Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);

        var trips = await db.Trips
            .AsNoTracking()
            .Where(x => x.IntermediaryAccountId == accountId && x.DriverUserId == userId)
            .Where(x => x.DepartureDate == today || x.DepartureDate == tomorrow)
            .OrderBy(x => x.DepartureDate)
            .ThenBy(x => x.DepartureTime)
            .Select(x => new DriverTripResponse(
                x.Id, x.TourTitle, x.DepartureDate, x.DepartureTime,
                x.VehicleNumber, x.Capacity))
            .ToListAsync();

        return Ok(trips);
    }

    private bool TryGetScope(out int userId, out int accountId, out string accountType, out string role)
    {
        userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId : 0;
        accountId = int.TryParse(User.FindFirstValue("AccountId"), out var parsedAccountId)
            ? parsedAccountId : 0;
        accountType = User.FindFirstValue("AccountType") ?? string.Empty;
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return userId > 0 && accountId > 0;
    }
}
