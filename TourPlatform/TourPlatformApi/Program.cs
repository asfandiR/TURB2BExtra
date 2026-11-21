using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.IdentityModel.Tokens;
using TourPlatformApi.Auth;
using TourPlatformApi.Data;
using TourPlatformApi.Domain;
using TourPlatformApi.Endpoints;
using TourPlatformApi.Notifications;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "TURB2BExtra API";
        return Task.CompletedTask;
    });
});
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddValidation();

var connectionString = builder.Configuration.GetConnectionString("PostgreSql")
    ?? "Host=localhost;Port=5432;Database=tour_platform;Username=postgres;Password=postgres";
builder.Services.AddDbContext<TourPlatformDbContext>(options =>
    options.UseNpgsql(connectionString));

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TURB2BExtra";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TURB2BExtra";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("JWT signing key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddSingleton<SubscriptionNotificationPolicy>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/login", async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> (
    LoginRequest request,
    TourPlatformDbContext db,
    IJwtService jwtService,
    CancellationToken cancellationToken) =>
{
    var user = await db.Users.Include(x => x.Account)
        .FirstOrDefaultAsync(x => x.Login == request.Login, cancellationToken);
    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        return TypedResults.Unauthorized();

    var response = jwtService.CreateToken(user);
    return TypedResults.Ok(response);
}).WithName("Login").WithSummary("Sign in and receive a JWT");

app.MapPlatformEndpoints();

await SeedDemoAccountsAsync(app.Services);
app.Run();

static async Task SeedDemoAccountsAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<TourPlatformDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Upgrade an existing demo database without requiring an external migration step.
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Accounts" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT 'Demo company';
        ALTER TABLE "Accounts" ADD COLUMN IF NOT EXISTS "ConnectionHexCode" character varying(16) NOT NULL DEFAULT '';
        ALTER TABLE "Accounts" ADD COLUMN IF NOT EXISTS "DefaultPriceCurrency" text NOT NULL DEFAULT 'USD';
        ALTER TABLE "Accounts" ADD COLUMN IF NOT EXISTS "WhatsAppTarget" text NULL;
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "Phone" text NULL;
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "Email" text NULL;
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "BaseSalary" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "CommissionPercent" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "CommissionBasis" integer NOT NULL DEFAULT 0;
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "PartnerConnections" (
            "Id" uuid PRIMARY KEY,
            "AgencyAccountId" uuid NOT NULL,
            "IntermediaryAccountId" uuid NOT NULL,
            "Status" text NOT NULL DEFAULT 'Active',
            "CreatedAt" timestamp with time zone NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartnerConnections_Pair"
            ON "PartnerConnections" ("AgencyAccountId", "IntermediaryAccountId");
        CREATE TABLE IF NOT EXISTS "WarehouseItems" (
            "Id" uuid PRIMARY KEY,
            "AccountId" uuid NOT NULL,
            "Title" text NOT NULL DEFAULT '',
            "CostPrice" numeric(18,2) NOT NULL DEFAULT 0,
            "SalePrice" numeric(18,2) NOT NULL DEFAULT 0,
            "Currency" text NOT NULL DEFAULT 'USD',
            "StockQty" numeric NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS "CashRegisterSales" (
            "Id" uuid PRIMARY KEY,
            "IntermediaryAccountId" uuid NOT NULL,
            "StaffUserId" uuid NOT NULL,
            "ItemId" uuid NULL,
            "Description" text NOT NULL DEFAULT '',
            "Amount" numeric(18,2) NOT NULL DEFAULT 0,
            "Currency" text NOT NULL DEFAULT 'USD',
            "CreatedAt" timestamp with time zone NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "Notifications" (
            "Id" uuid PRIMARY KEY,
            "AccountId" uuid NOT NULL,
            "RecipientUserId" uuid NULL,
            "Category" text NOT NULL DEFAULT 'New',
            "Title" text NOT NULL DEFAULT '',
            "Message" text NOT NULL DEFAULT '',
            "IsRead" boolean NOT NULL DEFAULT FALSE,
            "CreatedAt" timestamp with time zone NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "TripStaffAssignments" (
            "Id" uuid PRIMARY KEY,
            "TripId" uuid NOT NULL,
            "UserId" uuid NOT NULL,
            "Role" text NOT NULL DEFAULT 'Driver',
            "VehiclePlate" text NULL,
            "PickupZone" text NULL
        );
        CREATE TABLE IF NOT EXISTS "CompanyExpenses" (
            "Id" uuid PRIMARY KEY,
            "AccountId" uuid NOT NULL,
            "Department" text NOT NULL DEFAULT '',
            "CreatedByUserId" uuid NOT NULL,
            "Title" text NOT NULL DEFAULT '',
            "Category" text NOT NULL DEFAULT 'OneTime',
            "Amount" numeric(18,2) NOT NULL DEFAULT 0,
            "Currency" text NOT NULL DEFAULT 'USD'
        );
        CREATE TABLE IF NOT EXISTS "ExternalPartners" (
            "Id" uuid PRIMARY KEY,
            "AccountId" uuid NOT NULL,
            "Type" text NOT NULL DEFAULT '',
            "Name" text NOT NULL DEFAULT '',
            "Phone" text NULL
        );
        CREATE TABLE IF NOT EXISTS "CurrencyRates" (
            "Code" text PRIMARY KEY,
            "RateToUsd" numeric(18,8) NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );
        """);

    var agency = await db.Accounts.FirstOrDefaultAsync(x =>
        x.AccountType == AccountType.Agency && x.ConnectionHexCode == "A1B2C3D4");
    if (agency is null)
    {
        agency = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Golden Route Agency",
            AccountType = AccountType.Agency,
            Plan = SubscriptionPlan.Lux,
            ConnectionHexCode = "A1B2C3D4",
            DefaultPriceCurrency = Currency.USD,
            SeasonFullPrice = true,
            ActiveUntil = DateTimeOffset.UtcNow.AddYears(1)
        };
        db.Accounts.Add(agency);
    }

    var mediterranean = await db.Accounts.FirstOrDefaultAsync(x =>
        x.AccountType == AccountType.Intermediary && x.ConnectionHexCode == "F10E20A5");
    if (mediterranean is null)
    {
        mediterranean = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Mediterranean Trips",
            AccountType = AccountType.Intermediary,
            Plan = SubscriptionPlan.Unlimited,
            ConnectionHexCode = "F10E20A5",
            DefaultPriceCurrency = Currency.USD,
            SeasonFullPrice = true,
            ActiveUntil = DateTimeOffset.UtcNow.AddYears(1)
        };
        db.Accounts.Add(mediterranean);
    }

    var alanya = await db.Accounts.FirstOrDefaultAsync(x =>
        x.AccountType == AccountType.Intermediary && x.ConnectionHexCode == "B2C3D4E5");
    if (alanya is null)
    {
        alanya = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Alanya Premium Transfers",
            AccountType = AccountType.Intermediary,
            Plan = SubscriptionPlan.Medium,
            ConnectionHexCode = "B2C3D4E5",
            DefaultPriceCurrency = Currency.EUR,
            SeasonFullPrice = true,
            ActiveUntil = DateTimeOffset.UtcNow.AddYears(1)
        };
        db.Accounts.Add(alanya);
    }

    await db.SaveChangesAsync();

    await EnsureUserAsync(db, agency.Id, "agency.owner@demo.local", "AgencyOwner123!", "Owner Agency", UserRole.AgencyOwner, 1800m, 0m, CommissionBasis.OwnSales);
    await EnsureUserAsync(db, agency.Id, "agency.manager@demo.local", "AgencyManager123!", "Manager Agency", UserRole.AgencyManager, 1200m, 2m, CommissionBasis.OwnSales);
    await EnsureUserAsync(db, agency.Id, "agency.accountant@demo.local", "AgencyAccountant123!", "Accountant Agency", UserRole.AgencyAccountant, 1400m, 0m, CommissionBasis.OwnSales);
    await EnsureUserAsync(db, agency.Id, "guide@demo.local", "Guide123!", "Agency Guide", UserRole.AgencyGuide, 700m, 5m, CommissionBasis.OwnSales);

    await EnsureUserAsync(db, mediterranean.Id, "owner@demo.local", "Owner123!", "Mediterranean Owner", UserRole.IntermediaryOwner, 2500m, 0m, CommissionBasis.TotalCashRegister);
    await EnsureUserAsync(db, mediterranean.Id, "driver@demo.local", "Driver123!", "Mediterranean Driver", UserRole.IntermediaryDriver, 900m, 1m, CommissionBasis.OwnSales);
    await EnsureUserAsync(db, mediterranean.Id, "dispatcher.med@demo.local", "DispatcherMed123!", "Mediterranean Dispatcher", UserRole.IntermediaryDispatcher, 1300m, 2m, CommissionBasis.OwnSales);
    await EnsureUserAsync(db, mediterranean.Id, "sales.med@demo.local", "SalesMed123!", "Mediterranean Sales", UserRole.IntermediaryStaffSales, 800m, 4m, CommissionBasis.OwnSales);

    await EnsureUserAsync(db, alanya.Id, "owner.alanya@demo.local", "AlanyaOwner123!", "Alanya Premium Owner", UserRole.IntermediaryOwner, 2300m, 0m, CommissionBasis.TotalCashRegister);
    await EnsureUserAsync(db, alanya.Id, "dispatcher.alanya@demo.local", "AlanyaDispatcher123!", "Alanya Dispatcher", UserRole.IntermediaryDispatcher, 1250m, 2m, CommissionBasis.OwnSales);
    await EnsureUserAsync(db, alanya.Id, "sales.alanya@demo.local", "AlanyaSales123!", "Alanya Sales", UserRole.IntermediaryStaffSales, 750m, 5m, CommissionBasis.OwnSales);
    await EnsureUserAsync(db, alanya.Id, "driver.alanya@demo.local", "AlanyaDriver123!", "Alanya Driver", UserRole.IntermediaryDriver, 850m, 1m, CommissionBasis.OwnSales);

    await EnsureConnectionAsync(db, agency.Id, mediterranean.Id);
    await EnsureConnectionAsync(db, agency.Id, alanya.Id);

    if (!await db.Hotels.AnyAsync())
    {
        db.Hotels.AddRange(
            new Hotel { Id = Guid.NewGuid(), Region = "Alanya", Name = "Kaya Palazzo Resort", DefaultPickupNote = "Главный вход" },
            new Hotel { Id = Guid.NewGuid(), Region = "Alanya", Name = "Long Beach Resort", DefaultPickupNote = "Ресепшен" },
            new Hotel { Id = Guid.NewGuid(), Region = "Alanya", Name = "Granada Luxury Okurcalar", DefaultPickupNote = "Лобби" },
            new Hotel { Id = Guid.NewGuid(), Region = "Side", Name = "Side Star Elegance", DefaultPickupNote = "Главный вход" },
            new Hotel { Id = Guid.NewGuid(), Region = "Side", Name = "Barut Hemera", DefaultPickupNote = "Ресепшен" },
            new Hotel { Id = Guid.NewGuid(), Region = "Antalya", Name = "Akra Antalya", DefaultPickupNote = "Главный вход" },
            new Hotel { Id = Guid.NewGuid(), Region = "Antalya", Name = "Titanic Mardan Palace", DefaultPickupNote = "Лобби" },
            new Hotel { Id = Guid.NewGuid(), Region = "Belek", Name = "Calista Luxury Resort", DefaultPickupNote = "Главный вход" });
    }

    if (!await db.Tours.AnyAsync(x => x.IntermediaryAccountId == mediterranean.Id))
    {
        db.Tours.AddRange(
            new Tour { Id = Guid.NewGuid(), IntermediaryAccountId = mediterranean.Id, Title = "Корабельная экскурсия", PublicPrice = 35m, Currency = Currency.USD },
            new Tour { Id = Guid.NewGuid(), IntermediaryAccountId = mediterranean.Id, Title = "Старый город и пещеры", PublicPrice = 28m, Currency = Currency.EUR });
    }
    if (!await db.Tours.AnyAsync(x => x.IntermediaryAccountId == alanya.Id))
    {
        db.Tours.AddRange(
            new Tour { Id = Guid.NewGuid(), IntermediaryAccountId = alanya.Id, Title = "Замок Аланьи", PublicPrice = 950m, Currency = Currency.TRY },
            new Tour { Id = Guid.NewGuid(), IntermediaryAccountId = alanya.Id, Title = "Рафтинг в Кепрюлю", PublicPrice = 32m, Currency = Currency.EUR });
    }
    await db.SaveChangesAsync();

    var mediterraneanDriver = await db.Users.FirstAsync(x => x.Login == "driver@demo.local");
    var alanyaDriver = await db.Users.FirstAsync(x => x.Login == "driver.alanya@demo.local");
    var mediterraneanTours = await db.Tours.Where(x => x.IntermediaryAccountId == mediterranean.Id).ToListAsync();
    var alanyaTours = await db.Tours.Where(x => x.IntermediaryAccountId == alanya.Id).ToListAsync();

    if (!await db.Trips.AnyAsync(x => x.IntermediaryAccountId == mediterranean.Id))
    {
        for (var day = 0; day < 4; day++)
        {
            var tour = mediterraneanTours[day % mediterraneanTours.Count];
            db.Trips.Add(new Trip
            {
                Id = Guid.NewGuid(),
                IntermediaryAccountId = mediterranean.Id,
                TourId = tour.Id,
                DepartureDate = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(day), TimeSpan.Zero),
                DepartureTime = new TimeOnly(8 + day, 30),
                VehicleNumber = $"34 MED {100 + day}",
                DriverUserId = mediterraneanDriver.Id,
                Capacity = 35
            });
        }
    }
    if (!await db.Trips.AnyAsync(x => x.IntermediaryAccountId == alanya.Id))
    {
        for (var day = 0; day < 4; day++)
        {
            var tour = alanyaTours[day % alanyaTours.Count];
            db.Trips.Add(new Trip
            {
                Id = Guid.NewGuid(),
                IntermediaryAccountId = alanya.Id,
                TourId = tour.Id,
                DepartureDate = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(day), TimeSpan.Zero),
                DepartureTime = new TimeOnly(9 + day, 0),
                VehicleNumber = $"07 ALP {200 + day}",
                DriverUserId = alanyaDriver.Id,
                Capacity = 28
            });
        }
    }

    if (!await db.WarehouseItems.AnyAsync(x => x.AccountId == agency.Id))
    {
        db.WarehouseItems.Add(new WarehouseItem
        {
            Id = Guid.NewGuid(), AccountId = agency.Id, Title = "City sightseeing",
            CostPrice = 18, SalePrice = 25, Currency = Currency.USD, StockQty = 40
        });
    }
    if (!await db.WarehouseItems.AnyAsync(x => x.AccountId == mediterranean.Id))
    {
        db.WarehouseItems.Add(new WarehouseItem
        {
            Id = Guid.NewGuid(), AccountId = mediterranean.Id, Title = "Boat transfer",
            CostPrice = 30, SalePrice = 42, Currency = Currency.EUR, StockQty = 18
        });
    }
    if (!await db.WarehouseItems.AnyAsync(x => x.AccountId == alanya.Id))
    {
        db.WarehouseItems.Add(new WarehouseItem
        {
            Id = Guid.NewGuid(), AccountId = alanya.Id, Title = "Alanya Castle Tour",
            CostPrice = 22, SalePrice = 35, Currency = Currency.TRY, StockQty = 25
        });
    }
    if (!await db.Notifications.AnyAsync(x => x.AccountId == agency.Id))
    {
        db.Notifications.Add(new PlatformNotification
        {
            Id = Guid.NewGuid(), AccountId = agency.Id, Category = NotificationCategory.New,
            Title = "Система готова",
            Message = "Демо-агентство подключено к двум посредникам."
        });
    }

    foreach (var rate in new[]
    {
        (Currency.USD, 1m),
        (Currency.EUR, 1.08m),
        (Currency.GBP, 1.27m),
        (Currency.TRY, 0.029m)
    })
    {
        if (await db.CurrencyRates.AnyAsync(x => x.Code == rate.Item1))
            continue;
        db.CurrencyRates.Add(new CurrencyRate
        {
            Code = rate.Item1,
            RateToUsd = rate.Item2,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    await db.SaveChangesAsync();
}

static async Task EnsureUserAsync(TourPlatformDbContext db, Guid accountId, string login, string password,
    string fullName, UserRole role, decimal salary, decimal commissionPercent, CommissionBasis commissionBasis)
{
    var user = await db.Users.FirstOrDefaultAsync(x => x.Login == login);
    if (user is not null)
    {
        user.AccountId = accountId;
        user.FullName = fullName;
        user.Role = role;
        user.BaseSalary = salary;
        user.CommissionPercent = commissionPercent;
        user.CommissionBasis = commissionBasis;
        return;
    }

    db.Users.Add(new User
    {
        Id = Guid.NewGuid(),
        AccountId = accountId,
        FullName = fullName,
        Login = login,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Role = role,
        BaseSalary = salary,
        CommissionPercent = commissionPercent,
        CommissionBasis = commissionBasis
    });
}

static async Task EnsureConnectionAsync(TourPlatformDbContext db, Guid agencyId, Guid intermediaryId)
{
    if (await db.PartnerConnections.AnyAsync(x =>
        x.AgencyAccountId == agencyId && x.IntermediaryAccountId == intermediaryId))
        return;
    db.PartnerConnections.Add(new PartnerConnection
    {
        Id = Guid.NewGuid(),
        AgencyAccountId = agencyId,
        IntermediaryAccountId = intermediaryId,
        Status = "Active"
    });
}
