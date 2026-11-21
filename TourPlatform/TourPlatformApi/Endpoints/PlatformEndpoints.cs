using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using TourPlatformApi.Data;
using TourPlatformApi.Domain;

namespace TourPlatformApi.Endpoints;

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tours/my", async Task<Ok<TourResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Intermediary, out var accountId))
                return TypedResults.Ok(Array.Empty<TourResponse>());

            var tours = await db.Tours.AsNoTracking()
                .Where(x => x.IntermediaryAccountId == accountId)
                .OrderBy(x => x.Title)
                .Select(x => new TourResponse(x.Id, x.Title, x.PublicPrice, x.Currency))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(tours);
        }).RequireAuthorization().WithName("GetMyTours");

        app.MapPost("/api/tours", async Task<Results<Created<TourResponse>, UnauthorizedHttpResult, BadRequest<string>>> (
            CreateTourRequest request,
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Intermediary, out var accountId))
                return TypedResults.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Title) || request.PublicPrice < 0)
                return TypedResults.BadRequest("Название обязательно, цена не может быть отрицательной.");

            var tour = new Tour
            {
                Id = Guid.NewGuid(),
                IntermediaryAccountId = accountId,
                Title = request.Title.Trim(),
                PublicPrice = request.PublicPrice,
                Currency = request.Currency
            };
            db.Tours.Add(tour);
            await db.SaveChangesAsync(cancellationToken);
            var response = new TourResponse(tour.Id, tour.Title, tour.PublicPrice, tour.Currency);
            return TypedResults.Created($"/api/tours/{tour.Id}", response);
        }).RequireAuthorization().WithName("CreateTour");

        app.MapGet("/api/trips/available", async Task<Ok<AvailableTripResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Agency, out _))
                return TypedResults.Ok(Array.Empty<AvailableTripResponse>());

            var now = DateTimeOffset.UtcNow;
            var trips = await db.Trips.AsNoTracking()
                .Where(x => x.DepartureDate >= now)
                .OrderBy(x => x.DepartureDate)
                .Select(x => new AvailableTripResponse(
                    x.Id,
                    x.Tour.Title,
                    x.DepartureDate,
                    x.DepartureTime,
                    x.VehicleNumber,
                    x.Capacity,
                    x.Bookings.Sum(b => b.Adults + b.Children),
                    x.Tour.PublicPrice,
                    x.Tour.Currency))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(trips.Where(x => x.Capacity > x.BookedPeople).ToArray());
        }).RequireAuthorization().WithName("GetAvailableTrips");

        app.MapGet("/api/intermediary/trips", async Task<Ok<IntermediaryTripResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Intermediary, out var accountId))
                return TypedResults.Ok(Array.Empty<IntermediaryTripResponse>());

            var trips = await db.Trips.AsNoTracking()
                .Where(x => x.IntermediaryAccountId == accountId)
                .OrderBy(x => x.DepartureDate)
                .Select(x => new IntermediaryTripResponse(
                    x.Id,
                    x.Tour.Title,
                    x.DepartureDate,
                    x.DepartureTime,
                    x.VehicleNumber,
                    x.Capacity,
                    x.Bookings.Sum(b => b.Adults + b.Children),
                    x.Bookings.Sum(b => b.CashAmount),
                    x.Tour.Currency))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(trips);
        }).RequireAuthorization().WithName("GetIntermediaryTrips");

        app.MapGet("/api/intermediary/drivers", async Task<Ok<DriverResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Intermediary, out var accountId))
                return TypedResults.Ok(Array.Empty<DriverResponse>());

            var drivers = await db.Users.AsNoTracking()
                .Where(x => x.AccountId == accountId && x.Role == UserRole.IntermediaryDriver)
                .OrderBy(x => x.FullName)
                .Select(x => new DriverResponse(x.Id, x.FullName))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(drivers);
        }).RequireAuthorization().WithName("GetIntermediaryDrivers");

        app.MapGet("/api/hotels", async (
            string? region,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var query = db.Hotels.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(region))
                query = query.Where(x => EF.Functions.ILike(x.Region, $"%{region}%"));

            var hotels = await query.OrderBy(x => x.Region).ThenBy(x => x.Name)
                .Select(x => new HotelResponse(x.Id, x.Region, x.Name, x.DefaultPickupNote))
                .ToListAsync(cancellationToken);
            return TypedResults.Ok(hotels);
        }).RequireAuthorization().WithName("SearchHotels");

        app.MapPost("/api/trips", async Task<IResult> (
            CreateTripRequest request,
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Intermediary, out var accountId))
                return TypedResults.Unauthorized();

            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                IntermediaryAccountId = accountId,
                TourId = request.TourId,
                DepartureDate = request.DepartureDate,
                DepartureTime = request.DepartureTime,
                VehicleNumber = request.VehicleNumber ?? string.Empty,
                DriverUserId = request.DriverUserId,
                Capacity = request.Capacity
            };

            db.Trips.Add(trip);
            await db.SaveChangesAsync(cancellationToken);
            var response = new CreateTripResponse(trip.Id);
            return TypedResults.Created($"/api/trips/{trip.Id}", response);
        }).RequireAuthorization().WithName("CreateTrip");

        app.MapPost("/api/trips/{tripId:guid}/staff", async Task<Results<Created<TripStaffResponse>, NotFound, UnauthorizedHttpResult>> (
            Guid tripId,
            AddTripStaffRequest request,
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Intermediary, out var accountId))
                return TypedResults.Unauthorized();
            var trip = await db.Trips.FirstOrDefaultAsync(x =>
                x.Id == tripId && x.IntermediaryAccountId == accountId, cancellationToken);
            if (trip is null)
                return TypedResults.NotFound();
            var validUser = await db.Users.AnyAsync(x =>
                x.Id == request.UserId && x.AccountId == accountId, cancellationToken);
            if (!validUser)
                return TypedResults.NotFound();

            if (await db.TripStaffAssignments.AnyAsync(x =>
                x.TripId == tripId && x.UserId == request.UserId && x.Role == request.Role, cancellationToken))
                return TypedResults.Created($"/api/trips/{tripId}/staff",
                    new TripStaffResponse(tripId, request.UserId, request.Role, request.VehiclePlate, request.PickupZone));

            db.TripStaffAssignments.Add(new TripStaffAssignment
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                UserId = request.UserId,
                Role = request.Role,
                VehiclePlate = request.VehiclePlate,
                PickupZone = request.PickupZone
            });
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/trips/{tripId}/staff",
                new TripStaffResponse(tripId, request.UserId, request.Role, request.VehiclePlate, request.PickupZone));
        }).RequireAuthorization().WithName("AddTripStaff");

        app.MapGet("/api/trips/my", async Task<Ok<DriverTripResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var accountIdValue = principal.FindFirstValue("account_id");
            if (role != nameof(UserRole.IntermediaryDriver) || !Guid.TryParse(accountIdValue, out var accountId))
                return TypedResults.Ok(Array.Empty<DriverTripResponse>());

            var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var userId))
                return TypedResults.Ok(Array.Empty<DriverTripResponse>());

            var today = DateTimeOffset.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var trips = await db.Trips.AsNoTracking()
                .Where(x => x.IntermediaryAccountId == accountId &&
                            x.DriverUserId == userId &&
                            x.DepartureDate.Date >= today &&
                            x.DepartureDate.Date <= tomorrow)
                .Select(x => new DriverTripResponse(x.Id, x.DepartureDate, x.DepartureTime,
                    x.VehicleNumber, x.Tour.Title, x.Capacity,
                    x.Bookings.Sum(b => b.Adults + b.Children)))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(trips);
        }).RequireAuthorization().WithName("GetMyTrips");

        app.MapPost("/api/trips/{tripId:guid}/expenses", async Task<Results<Created<ExpenseResponse>, NotFound>> (
            Guid tripId,
            CreateExpenseRequest request,
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Intermediary, out var accountId))
                return TypedResults.NotFound();
            var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == tripId && x.IntermediaryAccountId == accountId, cancellationToken);
            if (trip is null)
                return TypedResults.NotFound();

            var expense = new TripExpense
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                Title = request.Title,
                BrickType = request.BrickType,
                Amount = request.Amount,
                Currency = request.Currency,
                IsPenaltyOrExtra = request.IsPenaltyOrExtra
            };
            db.TripExpenses.Add(expense);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/trips/{tripId}/expenses/{expense.Id}", new ExpenseResponse(expense.Id, expense.Title, expense.BrickType, expense.Amount, expense.Currency));
        }).RequireAuthorization().WithName("CreateTripExpense");

        app.MapGet("/api/trips/{tripId:guid}/cost", async Task<Results<Ok<TripCostResponse>, NotFound>> (
            Guid tripId,
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Intermediary, out var accountId))
                return TypedResults.NotFound();
            var trip = await db.Trips.AsNoTracking()
                .Include(x => x.Expenses)
                .Include(x => x.Bookings)
                .FirstOrDefaultAsync(x => x.Id == tripId && x.IntermediaryAccountId == accountId, cancellationToken);
            if (trip is null)
                return TypedResults.NotFound();

            var people = trip.Bookings.Sum(x => x.Adults + x.Children);
            var perTrip = trip.Expenses.Where(x => x.BrickType == ExpenseBrickType.PerTrip).Sum(x => x.Amount);
            var perPerson = trip.Expenses.Where(x => x.BrickType == ExpenseBrickType.PerPerson).Sum(x => x.Amount) * people;
            var revenue = trip.Bookings.Sum(x => x.CashAmount);
            var totalCost = perTrip + perPerson;
            var margin = revenue - totalCost;

            return TypedResults.Ok(new TripCostResponse(tripId, people, perTrip, perPerson, totalCost, revenue, margin));
        }).RequireAuthorization().WithName("GetTripCost");

        app.MapPost("/api/bookings", async Task<Results<Created<BookingResponse>, UnauthorizedHttpResult, ForbidHttpResult, NotFound, BadRequest<string>>> (
            CreateBookingRequest request,
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Agency, out var agencyAccountId))
                return TypedResults.Unauthorized();

            var role = principal.FindFirstValue(ClaimTypes.Role);
            var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var guideUserId) ||
                (role == nameof(UserRole.AgencyGuide) && guideUserId != request.GuideUserId))
                return TypedResults.Forbid();

            var trip = await db.Trips.Include(x => x.Tour)
                .FirstOrDefaultAsync(x => x.Id == request.TripId, cancellationToken);
            if (trip is null)
                return TypedResults.NotFound();
            if (request.Adults < 1 || request.TotalSaleAmount < 0 || request.PaidAmount < 0)
                return TypedResults.BadRequest("Проверьте состав и суммы билета.");

            var rateSnapshot = await db.CurrencyRates.AsNoTracking()
                .ToDictionaryAsync(x => x.Code, x => x.RateToUsd, cancellationToken);
            static decimal ToUsd(decimal amount, Currency currency, IReadOnlyDictionary<Currency, decimal> rates)
                => amount * (rates.TryGetValue(currency, out var rate) ? rate : 1m);
            static decimal FromUsd(decimal usd, Currency currency, IReadOnlyDictionary<Currency, decimal> rates)
                => rates.TryGetValue(currency, out var rate) && rate != 0m ? usd / rate : usd;

            var totalInUsd = ToUsd(request.TotalSaleAmount, request.SaleCurrency, rateSnapshot);
            var paidInUsd = ToUsd(request.PaidAmount, request.PaidCurrency, rateSnapshot);
            var unpaidInUsd = Math.Max(0m, totalInUsd - paidInUsd);
            var remainingAmount = FromUsd(unpaidInUsd, trip.Tour.Currency, rateSnapshot);

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                AgencyAccountId = agencyAccountId,
                IntermediaryAccountId = trip.IntermediaryAccountId,
                GuideUserId = request.GuideUserId,
                TripId = request.TripId,
                HotelId = request.HotelId,
                CustomPickupPoint = request.CustomPickupPoint,
                Adults = request.Adults,
                Children = request.Children,
                CommissionAmount = role == nameof(UserRole.AgencyGuide) ? 0 : request.CommissionAmount,
                CashAmount = request.PaidAmount,
                Currency = request.PaidCurrency,
                TotalSaleAmount = request.TotalSaleAmount,
                PaidAmount = request.PaidAmount,
                PaidCurrency = request.PaidCurrency,
                RemainingAmount = remainingAmount,
                RemainingCurrency = trip.Tour.Currency
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/bookings/{booking.Id}", new BookingResponse(
                booking.Id, booking.AgencyAccountId, booking.TripId, booking.GuideUserId,
                booking.HotelId, booking.CustomPickupPoint, booking.Adults, booking.Children,
                booking.CommissionAmount, booking.CashAmount, booking.Currency, booking.Status,
                booking.TotalSaleAmount, booking.PaidAmount, booking.RemainingAmount,
                booking.RemainingCurrency));
        }).RequireAuthorization().WithName("CreateBooking");

        app.MapGet("/api/bookings/my", async Task<Ok<BookingResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Agency, out var accountId))
                return TypedResults.Ok(Array.Empty<BookingResponse>());

            var role = principal.FindFirstValue(ClaimTypes.Role);
            var query = db.Bookings.AsNoTracking().Where(x => x.AgencyAccountId == accountId);
            if (role == nameof(UserRole.AgencyGuide))
            {
                var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdValue, out var guideUserId))
                    return TypedResults.Ok(Array.Empty<BookingResponse>());
                query = query.Where(x => x.GuideUserId == guideUserId);
            }

            var bookings = await query.Select(x => new BookingResponse(
                x.Id, x.AgencyAccountId, x.TripId, x.GuideUserId, x.HotelId,
                x.CustomPickupPoint, x.Adults, x.Children, x.CommissionAmount,
                x.CashAmount, x.Currency, x.Status, x.TotalSaleAmount, x.PaidAmount,
                x.RemainingAmount, x.RemainingCurrency)).ToArrayAsync(cancellationToken);
            return TypedResults.Ok(bookings);
        }).RequireAuthorization().WithName("GetMyBookings");

        app.MapGet("/api/staff", async Task<Ok<StaffResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Agency, out var agencyId) &&
                !TryGetAccount(principal, AccountType.Intermediary, out agencyId))
                return TypedResults.Ok(Array.Empty<StaffResponse>());

            var staff = await db.Users.AsNoTracking()
                .Where(x => x.AccountId == agencyId)
                .Select(x => new StaffResponse(x.Id, x.FullName, x.Phone, x.Email, x.Role, x.BaseSalary,
                    x.CommissionPercent, x.CommissionBasis))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(staff);
        }).RequireAuthorization().WithName("GetStaff");

        app.MapPost("/api/partners/connect", async Task<Results<Ok<PartnerConnectionResponse>, BadRequest<string>, NotFound>> (
            ConnectPartnerRequest request,
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Agency, out var agencyId))
                return TypedResults.BadRequest("Подключение доступно сотрудникам агентства.");

            var hex = request.HexCode.Trim().ToUpperInvariant();
            var intermediary = await db.Accounts.FirstOrDefaultAsync(x =>
                x.AccountType == AccountType.Intermediary && x.ConnectionHexCode == hex, cancellationToken);
            if (intermediary is null)
                return TypedResults.NotFound();

            if (await db.PartnerConnections.AnyAsync(x =>
                x.AgencyAccountId == agencyId && x.IntermediaryAccountId == intermediary.Id, cancellationToken))
                return TypedResults.BadRequest("Эта компания уже подключена.");

            var connection = new PartnerConnection
            {
                Id = Guid.NewGuid(),
                AgencyAccountId = agencyId,
                IntermediaryAccountId = intermediary.Id
            };
            db.PartnerConnections.Add(connection);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(new PartnerConnectionResponse(connection.Id, intermediary.Name, intermediary.ConnectionHexCode, connection.Status));
        }).RequireAuthorization().WithName("ConnectPartner");

        app.MapGet("/api/partners", async Task<Ok<PartnerConnectionResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Agency, out var agencyId))
                return TypedResults.Ok(Array.Empty<PartnerConnectionResponse>());

            var partners = await db.PartnerConnections.AsNoTracking()
                .Where(x => x.AgencyAccountId == agencyId)
                .Join(db.Accounts, connection => connection.IntermediaryAccountId, account => account.Id,
                    (connection, account) => new PartnerConnectionResponse(
                        connection.Id, account.Name, account.ConnectionHexCode, connection.Status))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(partners);
        }).RequireAuthorization().WithName("GetPartners");

        app.MapGet("/api/warehouse", async Task<Ok<WarehouseResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            Guid accountId;
            if (!TryGetAccount(principal, AccountType.Agency, out accountId) &&
                !TryGetAccount(principal, AccountType.Intermediary, out accountId))
                return TypedResults.Ok(Array.Empty<WarehouseResponse>());

            var items = await db.WarehouseItems.AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .Select(x => new WarehouseResponse(x.Id, x.Title, x.CostPrice, x.SalePrice, x.Currency, x.StockQty))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(items);
        }).RequireAuthorization().WithName("GetWarehouse");

        app.MapPost("/api/warehouse", async Task<Results<Created<WarehouseResponse>, UnauthorizedHttpResult>> (
            CreateWarehouseRequest request,
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            Guid accountId;
            if (!TryGetAccount(principal, AccountType.Agency, out accountId) &&
                !TryGetAccount(principal, AccountType.Intermediary, out accountId))
                return TypedResults.Unauthorized();

            var item = new WarehouseItem
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Title = request.Title.Trim(),
                CostPrice = request.CostPrice,
                SalePrice = request.SalePrice,
                Currency = request.Currency,
                StockQty = request.StockQty
            };
            db.WarehouseItems.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/warehouse/{item.Id}",
                new WarehouseResponse(item.Id, item.Title, item.CostPrice, item.SalePrice, item.Currency, item.StockQty));
        }).RequireAuthorization().WithName("CreateWarehouseItem");

        app.MapGet("/api/cash-register", async Task<Ok<CashRegisterResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Intermediary, out var accountId))
                return TypedResults.Ok(Array.Empty<CashRegisterResponse>());

            var sales = await db.CashRegisterSales.AsNoTracking()
                .Where(x => x.IntermediaryAccountId == accountId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CashRegisterResponse(x.Id, x.Description, x.Amount, x.Currency, x.CreatedAt))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(sales);
        }).RequireAuthorization().WithName("GetCashRegister");

        app.MapPost("/api/cash-register", async Task<Results<Created<CashRegisterResponse>, UnauthorizedHttpResult>> (
            CreateCashRegisterRequest request,
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAccount(principal, AccountType.Intermediary, out var accountId))
                return TypedResults.Unauthorized();
            var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var userId))
                return TypedResults.Unauthorized();

            var sale = new CashRegisterSale
            {
                Id = Guid.NewGuid(),
                IntermediaryAccountId = accountId,
                StaffUserId = userId,
                ItemId = request.ItemId,
                Description = request.Description.Trim(),
                Amount = request.Amount,
                Currency = request.Currency
            };
            db.CashRegisterSales.Add(sale);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/cash-register/{sale.Id}",
                new CashRegisterResponse(sale.Id, sale.Description, sale.Amount, sale.Currency, sale.CreatedAt));
        }).RequireAuthorization().WithName("CreateCashRegisterSale");

        app.MapGet("/api/notifications", async Task<Ok<NotificationResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            Guid accountId;
            if (!TryGetAccount(principal, AccountType.Agency, out accountId) &&
                !TryGetAccount(principal, AccountType.Intermediary, out accountId))
                return TypedResults.Ok(Array.Empty<NotificationResponse>());

            var notifications = await db.Notifications.AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new NotificationResponse(x.Id, x.Category, x.Title, x.Message, x.IsRead, x.CreatedAt))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(notifications);
        }).RequireAuthorization().WithName("GetNotifications");

        app.MapGet("/api/company-expenses", async Task<Ok<CompanyExpenseResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            Guid accountId;
            if (!TryGetAccount(principal, AccountType.Agency, out accountId) &&
                !TryGetAccount(principal, AccountType.Intermediary, out accountId))
                return TypedResults.Ok(Array.Empty<CompanyExpenseResponse>());

            var expenses = await db.CompanyExpenses.AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .OrderByDescending(x => x.Id)
                .Select(x => new CompanyExpenseResponse(x.Id, x.Department, x.CreatedByUserId, x.Title,
                    x.Category, x.Amount, x.Currency))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(expenses);
        }).RequireAuthorization().WithName("GetCompanyExpenses");

        app.MapPost("/api/company-expenses", async Task<Results<Created<CompanyExpenseResponse>, UnauthorizedHttpResult>> (
            CreateCompanyExpenseRequest request,
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            Guid accountId;
            if (!TryGetAccount(principal, AccountType.Agency, out accountId) &&
                !TryGetAccount(principal, AccountType.Intermediary, out accountId))
                return TypedResults.Unauthorized();
            var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var userId))
                return TypedResults.Unauthorized();

            var expense = new CompanyExpense
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Department = request.Department.Trim(),
                CreatedByUserId = userId,
                Title = request.Title.Trim(),
                Category = request.Category,
                Amount = request.Amount,
                Currency = request.Currency
            };
            db.CompanyExpenses.Add(expense);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/company-expenses/{expense.Id}",
                new CompanyExpenseResponse(expense.Id, expense.Department, expense.CreatedByUserId,
                    expense.Title, expense.Category, expense.Amount, expense.Currency));
        }).RequireAuthorization().WithName("CreateCompanyExpense");

        app.MapGet("/api/external-partners", async Task<Ok<ExternalPartnerResponse[]>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            Guid accountId;
            if (!TryGetAccount(principal, AccountType.Agency, out accountId) &&
                !TryGetAccount(principal, AccountType.Intermediary, out accountId))
                return TypedResults.Ok(Array.Empty<ExternalPartnerResponse>());

            var partners = await db.ExternalPartners.AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .Select(x => new ExternalPartnerResponse(x.Id, x.Type, x.Name, x.Phone))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(partners);
        }).RequireAuthorization().WithName("GetExternalPartners");

        app.MapGet("/api/currency-rates", async Task<Ok<CurrencyRateResponse[]>> (
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var rates = await db.CurrencyRates.AsNoTracking()
                .OrderBy(x => x.Code)
                .Select(x => new CurrencyRateResponse(x.Code, x.RateToUsd, x.UpdatedAt))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(rates);
        }).RequireAuthorization().WithName("GetCurrencyRates");

        app.MapGet("/api/dashboard/owner", async Task<Ok<OwnerDashboardResponse>> (
            ClaimsPrincipal principal,
            TourPlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            Guid accountId;
            if (!TryGetAccount(principal, AccountType.Agency, out accountId) &&
                !TryGetAccount(principal, AccountType.Intermediary, out accountId))
                return TypedResults.Ok(new OwnerDashboardResponse(0, 0, 0, 0));

            var bookings = await db.Bookings.AsNoTracking()
                .Where(x => x.AgencyAccountId == accountId || x.IntermediaryAccountId == accountId)
                .ToArrayAsync(cancellationToken);
            var salesCount = bookings.Length;
            var touristCount = bookings.Sum(x => x.Adults + x.Children);
            var unpaid = bookings.Sum(x => x.RemainingAmount);
            var received = bookings.Sum(x => x.PaidAmount);
            return TypedResults.Ok(new OwnerDashboardResponse(salesCount, touristCount, received, unpaid));
        }).RequireAuthorization().WithName("GetOwnerDashboard");
    }

    private static bool TryGetAccount(ClaimsPrincipal principal, AccountType expectedType, out Guid accountId)
    {
        accountId = Guid.Empty;
        var type = principal.FindFirstValue("account_type");
        var id = principal.FindFirstValue("account_id");
        return Enum.TryParse<AccountType>(type, out var accountType) &&
               accountType == expectedType &&
               Guid.TryParse(id, out accountId);
    }
}

/// <summary>Request to create a trip.</summary>
public sealed record CreateTripRequest(
    Guid TourId,
    DateTimeOffset DepartureDate,
    TimeOnly DepartureTime,
    string? VehicleNumber,
    Guid? DriverUserId,
    int Capacity);

/// <summary>Hotel returned by the API.</summary>
public sealed record HotelResponse(Guid Id, string Region, string Name, string DefaultPickupNote);

/// <summary>Trip identifier returned after creation.</summary>
public sealed record CreateTripResponse(Guid Id);

/// <summary>Trip information visible to an assigned driver.</summary>
public sealed record DriverTripResponse(Guid Id, DateTimeOffset DepartureDate, TimeOnly DepartureTime, string VehicleNumber, string TourTitle, int Capacity, int BookedPeople);

/// <summary>Request to add an expense brick to a trip.</summary>
public sealed record CreateExpenseRequest(string Title, ExpenseBrickType BrickType, decimal Amount, Currency Currency, bool IsPenaltyOrExtra);

/// <summary>Expense brick returned by the API.</summary>
public sealed record ExpenseResponse(Guid Id, string Title, ExpenseBrickType BrickType, decimal Amount, Currency Currency);

/// <summary>Dynamic trip cost and margin calculation.</summary>
public sealed record TripCostResponse(Guid TripId, int PeopleCount, decimal PerTripCost, decimal PerPersonCost, decimal TotalCost, decimal Revenue, decimal Margin);

/// <summary>Request to create an agency booking.</summary>
public sealed record CreateBookingRequest(
    Guid TripId,
    Guid GuideUserId,
    Guid? HotelId,
    string? CustomPickupPoint,
    int Adults,
    int Children,
    decimal CommissionAmount,
    decimal TotalSaleAmount,
    Currency SaleCurrency,
    decimal PaidAmount,
    Currency PaidCurrency);

/// <summary>Booking returned by the API.</summary>
public sealed record BookingResponse(
    Guid Id,
    Guid AgencyAccountId,
    Guid TripId,
    Guid GuideUserId,
    Guid? HotelId,
    string? CustomPickupPoint,
    int Adults,
    int Children,
    decimal CommissionAmount,
    decimal CashAmount,
    Currency Currency,
    string Status,
    decimal TotalSaleAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    Currency RemainingCurrency);

/// <summary>Tour visible to the owning intermediary account.</summary>
public sealed record TourResponse(Guid Id, string Title, decimal PublicPrice, Currency Currency);

/// <summary>Request to create a tour offered by an intermediary.</summary>
public sealed record CreateTourRequest(string Title, decimal PublicPrice, Currency Currency);

/// <summary>Upcoming trip with remaining capacity available to agencies.</summary>
public sealed record AvailableTripResponse(
    Guid Id,
    string TourTitle,
    DateTimeOffset DepartureDate,
    TimeOnly DepartureTime,
    string VehicleNumber,
    int Capacity,
    int BookedPeople,
    decimal PublicPrice,
    Currency Currency);

/// <summary>Trip information visible to the owning intermediary account.</summary>
public sealed record IntermediaryTripResponse(
    Guid Id,
    string TourTitle,
    DateTimeOffset DepartureDate,
    TimeOnly DepartureTime,
    string VehicleNumber,
    int Capacity,
    int BookedPeople,
    decimal Revenue,
    Currency Currency);

/// <summary>Driver assigned to an intermediary account.</summary>
public sealed record DriverResponse(Guid Id, string FullName);

public sealed record StaffResponse(Guid Id, string FullName, string? Phone, string? Email, UserRole Role,
    decimal BaseSalary, decimal CommissionPercent, CommissionBasis CommissionBasis);
public sealed record ConnectPartnerRequest(string HexCode);
public sealed record PartnerConnectionResponse(Guid Id, string Name, string HexCode, string Status);
public sealed record WarehouseResponse(Guid Id, string Title, decimal CostPrice, decimal SalePrice, Currency Currency, decimal StockQty);
public sealed record CreateWarehouseRequest(string Title, decimal CostPrice, decimal SalePrice, Currency Currency, decimal StockQty);
public sealed record CashRegisterResponse(Guid Id, string Description, decimal Amount, Currency Currency, DateTimeOffset CreatedAt);
public sealed record CreateCashRegisterRequest(Guid? ItemId, string Description, decimal Amount, Currency Currency);
public sealed record NotificationResponse(Guid Id, NotificationCategory Category, string Title, string Message, bool IsRead, DateTimeOffset CreatedAt);
public sealed record CompanyExpenseResponse(Guid Id, string Department, Guid CreatedByUserId, string Title,
    ExpenseCategory Category, decimal Amount, Currency Currency);
public sealed record CreateCompanyExpenseRequest(string Department, string Title, ExpenseCategory Category,
    decimal Amount, Currency Currency);
public sealed record ExternalPartnerResponse(Guid Id, string Type, string Name, string? Phone);
public sealed record CurrencyRateResponse(Currency Code, decimal RateToUsd, DateTimeOffset UpdatedAt);
public sealed record OwnerDashboardResponse(int SalesCount, int TouristCount, decimal ReceivedAmount, decimal UnpaidAmount);
public sealed record AddTripStaffRequest(Guid UserId, string Role, string? VehiclePlate, string? PickupZone);
public sealed record TripStaffResponse(Guid TripId, Guid UserId, string Role, string? VehiclePlate, string? PickupZone);
