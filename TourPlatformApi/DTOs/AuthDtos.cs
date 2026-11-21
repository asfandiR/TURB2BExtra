using System.ComponentModel.DataAnnotations;
using TourPlatformApi.Models;

namespace TourPlatformApi.DTOs;

public sealed record LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);

public sealed record GuideBookingResponse(
    int Id,
    int AgencyAccountId,
    int? IntermediaryAccountId,
    int? TripId,
    int? HotelId,
    string? CustomPickupPoint,
    int Adults,
    int Children,
    decimal TicketPrice,
    decimal GuideCommission,
    decimal PaidAmount,
    decimal RestAmount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record CreateGuideBookingRequest(
    int TripId,
    int? HotelId,
    string? CustomPickupPoint,
    int Adults,
    int Children,
    decimal TicketPrice,
    decimal PaidAmount,
    Currency Currency);

public sealed record DriverTripResponse(
    int Id,
    string TourTitle,
    DateOnly DepartureDate,
    TimeOnly DepartureTime,
    string VehicleNumber,
    int Capacity);
