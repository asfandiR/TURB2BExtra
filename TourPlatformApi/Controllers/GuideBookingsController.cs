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
[Route("api/guide/bookings")]
public sealed class GuideBookingsController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<GuideBookingResponse>> Create(CreateGuideBookingRequest request)
    {
        if (!TryGetScope(out var userId, out var accountId, out var accountType, out var role) ||
            accountType != AccountType.Agency.ToString() || role != UserRole.AgencyGuide.ToString())
            return Forbid();
        if (request.Adults < 1 || request.TicketPrice < 0 || request.PaidAmount < 0)
            return BadRequest(new { message = "Проверьте состав группы и суммы билета." });

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == request.TripId);
        if (trip is null)
            return NotFound(new { message = "Рейс не найден." });
        if (request.HotelId.HasValue &&
            !await db.Hotels.AnyAsync(x => x.Id == request.HotelId.Value))
            return BadRequest(new { message = "Отель не найден." });

        var currentPeople = await db.Bookings
            .Where(x => x.TripId == request.TripId && x.Status != BookingStatus.Cancelled)
            .SumAsync(x => x.Adults + x.Children);
        var bookingPeople = request.Adults + request.Children;
        if (currentPeople + bookingPeople > trip.Capacity)
            return BadRequest(new { message = "Недостаточно свободных мест на рейсе." });

        var restAmount = Math.Max(0m, request.TicketPrice - request.PaidAmount);
        var booking = new Booking
        {
            AgencyAccountId = accountId,
            IntermediaryAccountId = trip.IntermediaryAccountId,
            GuideUserId = userId,
            TripId = trip.Id,
            HotelId = request.HotelId,
            CustomPickupPoint = request.CustomPickupPoint,
            Adults = request.Adults,
            Children = request.Children,
            TicketPrice = request.TicketPrice,
            GuideCommission = 0,
            PaidAmount = request.PaidAmount,
            RestAmount = restAmount,
            Currency = request.Currency,
            Status = BookingStatus.Confirmed
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        return Ok(new GuideBookingResponse(
            booking.Id, booking.AgencyAccountId, booking.IntermediaryAccountId, booking.TripId,
            booking.HotelId, booking.CustomPickupPoint, booking.Adults, booking.Children,
            booking.TicketPrice, booking.GuideCommission, booking.PaidAmount, booking.RestAmount,
            booking.Currency.ToString(), booking.Status.ToString(), booking.CreatedAt));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GuideBookingResponse>>> GetMyBookings()
    {
        if (!TryGetScope(out var userId, out var accountId, out var accountType, out var role) ||
            accountType != AccountType.Agency.ToString() || role != UserRole.AgencyGuide.ToString())
        {
            return Forbid();
        }

        var bookings = await db.Bookings
            .AsNoTracking()
            .Where(x => x.AgencyAccountId == accountId && x.GuideUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new GuideBookingResponse(
                x.Id, x.AgencyAccountId, x.IntermediaryAccountId, x.TripId, x.HotelId,
                x.CustomPickupPoint, x.Adults, x.Children, x.TicketPrice,
                x.GuideCommission, x.PaidAmount, x.RestAmount, x.Currency.ToString(),
                x.Status.ToString(), x.CreatedAt))
            .ToListAsync();

        return Ok(bookings);
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
