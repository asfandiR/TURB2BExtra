using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourPlatformApi.Data;

namespace TourPlatformApi.Controllers;

[ApiController]
[Authorize]
[Route("api/hotels")]
public sealed class HotelsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HotelResponse>>> GetAll()
    {
        var hotels = await db.Hotels
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new HotelResponse(x.Id, x.Name, x.Address))
            .ToListAsync();
        return Ok(hotels);
    }
}

public sealed record HotelResponse(int Id, string Name, string? Address);
