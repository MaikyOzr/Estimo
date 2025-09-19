using Estimo.Application.Features.Auth;
using Estimo.Application.Features.Client.Command;
using Estimo.Application.Features.Client.Query;
using Estimo.Application.Features.Client.Request;
using Estimo.Application.Features.Quote.Command;
using Estimo.Application.Features.Quote.Query;
using Estimo.Application.Features.Quote.Request;
using Estimo.Application.Service;
using Estimo.Domain;
using Estimo.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using Serilog;
using Stripe;
using Stripe.Checkout;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;


QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddHealthChecks();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("tight", opts =>
    {
        opts.Window = TimeSpan.FromSeconds(10);
        opts.PermitLimit = 50;
        opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opts.QueueLimit = 0;
    });
});

builder.Services.AddSwaggerGen(x =>
{
    x.SwaggerDoc("v1", new OpenApiInfo { Title = "Estimo API", Version = "v1" });
    x.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}"
    });
    x.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme{ Reference = new OpenApiReference{ Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var cs = builder.Configuration.GetConnectionString("pg")
         ?? Environment.GetEnvironmentVariable("ConnectionStrings__pg")
         ?? "Host=localhost;Port=5432;Database=estimo;Username=estimo;Password=estimo";
builder.Services.AddDbContext<EstimoDbContext>(o => o.UseNpgsql(cs));

builder.Services.AddScoped<CreateClientCommand>();
builder.Services.AddScoped<GetClientByIIdQuery>();
builder.Services.AddScoped<CreateQuoteCommand>();
builder.Services.AddScoped<GetQuoteByIdQuery>();
builder.Services.AddScoped<QuotePdfService>();
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
              ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(o => o.AddPolicy("allow-ui", p => p
    .WithOrigins(origins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("X-Request-ID")));



builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var jwt = builder.Configuration.GetSection("Jwt");
var keyString = jwt["Key"];
if (string.IsNullOrWhiteSpace(keyString)) throw new InvalidOperationException("Missing Jwt:Key");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero
        };
    });

var stripe = builder.Configuration.GetSection("Stripe");
StripeConfiguration.ApiKey = stripe["ApiKey"];


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EstimoDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();

app.Use(async (ctx, next) =>
{
    var raw = ctx.Request.Headers.TryGetValue("X-Request-ID", out var v) ? v.ToString() : null;

    string NewId() => Guid.NewGuid().ToString("N");
    var cid = string.IsNullOrWhiteSpace(raw) ? NewId() : raw.Trim();

    bool Bad(char ch) => ch <= 0x20 || ch == 0x7F || ch >= 0x80;
    if (cid.Any(Bad)) cid = NewId();

    ctx.Response.Headers["X-Request-ID"] = cid;

    await next();
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

(string token, DateTime exp) IssueToken(Guid userId, string email)
{
    var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, email)
    };

    var jwtToken = new JwtSecurityToken(
        issuer: jwt["Issuer"], audience: jwt["Audience"],
        claims: claims, expires: DateTime.UtcNow.AddHours(2), signingCredentials: creds);

    return (new JwtSecurityTokenHandler().WriteToken(jwtToken), jwtToken.ValidTo);
}

static (int dailyCap, bool unlimited) PlanCaps(string plan) => plan switch
{
    "business" => (int.MaxValue, true),
    "pro" => (100, false),
    _ => (5, false) // free
};

static async Task<(bool allowed, string reason)> CanGenerateAsync(EstimoDbContext db, Guid userId, CancellationToken ct)
{
    var billing = await db.UserBillings.FindAsync(new object[] { userId }, ct)
                  ?? new UserBilling { UserId = userId, Plan = "free" };

    // якщо період сабскрипції закінчився — повертаємося на free
    if (billing.CurrentPeriodEndUtc is DateTime end && end < DateTime.UtcNow)
        billing.Plan = "free";

    var usage = await db.UserUsages.FindAsync(new object[] { userId }, ct)
                ?? new UserUsage { UserId = userId };

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    if (usage.WindowDate != today) { usage.WindowDate = today; usage.DailyCount = 0; }

    var (cap, unlimited) = PlanCaps(billing.Plan);

    if (billing.Plan == "free")
    {
        if (usage.TotalCount < 15) return (true, "");
        if (usage.DailyCount >= 5) return (false, "Daily free limit reached (5/day).");
        return (true, "");
    }

    if (!unlimited && usage.DailyCount >= cap) return (false, $"Daily limit reached ({cap}/day).");
    return (true, "");
}

static void CommitGenerate(EstimoDbContext db, Guid userId)
{
    var usage = db.UserUsages.Find(userId) ?? new UserUsage { UserId = userId };
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    if (usage.WindowDate != today) { usage.WindowDate = today; usage.DailyCount = 0; }
    usage.TotalCount += 1;
    usage.DailyCount += 1;
    db.UserUsages.Update(usage);
}

app.MapHealthChecks("/health");
app.UseCors("allow-ui");
var clientsMap = app.MapGroup("/clients").RequireAuthorization();
var quoteMap = app.MapGroup("/quotes").RequireAuthorization();
var authMap = app.MapGroup("/auth").AllowAnonymous();
var billing = app.MapGroup("/billing").RequireAuthorization();

authMap.MapPost("/register", async (RegisterRequest req, IAuthService auth, CancellationToken ct) =>
{
    if (req.Password != req.confPassword)
        return Results.BadRequest(new { error = "Passwords do not match" });

    var userId = await auth.RegisterAsync(req.Email, req.Password, ct); // Guid
    var (tok, exp) = IssueToken(userId, req.Email);
    return Results.Created($"/api/users/{userId}", new AuthResponse(tok, exp, userId.ToString()));
});

authMap.MapPost("/login", async (LoginRequest req, IAuthService auth, CancellationToken ct) =>
{
    var user = await auth.FindByEmailAsync(req.Email, ct);
    if (user is null || !auth.VerifyPassword(user, req.Password)) return Results.Unauthorized();
    var (tok, exp) = IssueToken(user.Id, req.Email);
    return Results.Ok(new AuthResponse(tok, exp, user.Id.ToString()));
});

clientsMap.MapPost("/", async (CreateClientRequest request, CreateClientCommand command) =>
{
    var client = await command.Handle(request, CancellationToken.None);
    return Results.Created($"/clients/{client.Id}", client);
});

clientsMap.MapGet("/{id:guid}", async (Guid id, GetClientByIIdQuery query) =>
{
    var client = await query.Handle(id, CancellationToken.None);
    return Results.Ok(client);
});

quoteMap.MapPost("/", async (CreateQuoteRequest request, CreateQuoteCommand commnad) => {
    var quote = await commnad.Handle(request, CancellationToken.None);
    return Results.Created($"/quotes/{quote.Id}", quote);
});

quoteMap.MapPost("/{id:guid}/paylink", async (
    Guid id,
    EstimoDbContext db,
    ICurrentUser cur,
    CancellationToken ct) =>
{
    // 1) дістаємо квоту + перевіряємо власника
    var data = await (from q in db.Quotes
                      join c in db.Clients on q.ClientId equals c.Id
                      where q.Id == id && c.OwnerId == cur.Id
                      select new { q, c }).FirstOrDefaultAsync(ct);
    if (data is null) return Results.NotFound();

    // 2) total у євро → у центи
    var total = data.q.Amount + data.q.Amount * data.q.VatPercent / 100m;
    var unitAmount = (long)Math.Round(total * 100m, MidpointRounding.AwayFromZero);

    // 3) створюємо сесію
    var opts = new SessionCreateOptions
    {
        Mode = "payment",
        LineItems = new()
        {
            new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "eur",
                    UnitAmount = unitAmount,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"Quote {data.q.Name}"
                    }
                }
            }
        },
        SuccessUrl = $"http://localhost:5173/?pay=success&quoteId={id}&session_id={{CHECKOUT_SESSION_ID}}",
        CancelUrl = $"http://localhost:5173/?pay=cancel&quoteId={id}",
        Metadata = new Dictionary<string, string?>
        {
            ["userId"] = cur.Id.ToString(),
            ["quoteId"] = id.ToString()
        }
    };

    var session = await new Stripe.Checkout.SessionService().CreateAsync(opts, null, ct);

    // 4) зберігаємо
    data.q.PaymentUrl = session.Url;
    data.q.PaymentSessionId = session.Id;
    data.q.PaymentStatus = "pending";
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { url = session.Url });
});

quoteMap.MapGet("/{id:guid}", async(Guid id, GetQuoteByIdQuery query) => {
    var quote = await query.Handle(id, CancellationToken.None);
    return Results.Ok(quote);
});

quoteMap.MapPost("/confirm", async (
    string session_id,
    EstimoDbContext db,
    ICurrentUser cur,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(session_id)) return Results.BadRequest();

    var svc = new SessionService();
    var sess = await svc.GetAsync(session_id,
        options: null, requestOptions: null, cancellationToken: ct);

    var status = sess.Status?.ToString();
    var payment = sess.PaymentStatus?.ToString();
    if (!string.Equals(status, "complete", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(payment, "paid", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Not paid" });

    // знайдемо квоту за session_id + перевіримо власника
    var quote = await (from q in db.Quotes
                       join c in db.Clients on q.ClientId equals c.Id
                       where q.PaymentSessionId == session_id && c.OwnerId == cur.Id
                       select q).FirstOrDefaultAsync(ct);
    if (quote is null) return Results.Forbid();

    quote.PaymentStatus = "paid";
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { ok = true });
}).RequireAuthorization();

quoteMap.MapGet("/{id:guid}/pdf", async (Guid id, QuotePdfService service, EstimoDbContext db, ICurrentUser user, CancellationToken ct) =>
{
    var (ok, _) = await CanGenerateAsync(db, user.Id, ct);
    if (!ok) return Results.StatusCode(429);

    var pdf = await service.GeneratePdfAsync(id, ct);

    await QuotePdfService.CommitGenerateAsync(db, user.Id, ct);   // <-- зберігає всередині
    return Results.File(pdf, "application/pdf", $"quote-{id.ToString()[..8]}.pdf");
});

billing.MapPost("/checkout", async (CheckoutReq req, HttpContext ctx) =>
{
    var plan = (req.Plan ?? "pro").ToLowerInvariant();
    if (plan != "pro" && plan != "business") plan = "pro";

    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirstValue("sub");
    if (!Guid.TryParse(userId, out var uid)) return Results.Unauthorized();

    // ціни (у центах/копійках)
    var (amount, name) = plan == "business" ? (2900L, "Business (monthly)") : (900L, "Pro (monthly)");

    var options = new SessionCreateOptions
    {
        Mode = "subscription",
        LineItems = new()
        {
            new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "eur",
                    UnitAmount = amount,
                    Recurring = new SessionLineItemPriceDataRecurringOptions { Interval = "month", IntervalCount = 1 },
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = name }
                }
            }
        },
        SuccessUrl = $"http://localhost:5173/?billing=success&plan={plan}&session_id={{CHECKOUT_SESSION_ID}}",
        CancelUrl = $"http://localhost:5173/?billing=cancel",
        Metadata = new Dictionary<string, string?>
        {
            ["userId"] = uid.ToString(),
            ["plan"] = plan
        }
    };

    var session = await new SessionService().CreateAsync(options);
    return Results.Ok(new { url = session.Url });
});

billing.MapPost("/confirm", async (
    string session_id,
    ICurrentUser cur,
    EstimoDbContext db,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(session_id))
        return Results.BadRequest();

    var svc = new SessionService();
    var sess = await svc.GetAsync(
        session_id,
        options: new SessionGetOptions { Expand = new List<string> { "subscription" } },
        requestOptions: null,
        cancellationToken: ct);

    // Узгоджено для різних версій Stripe.NET
    var status = sess.Status?.ToString();
    var payment = sess.PaymentStatus?.ToString();

    if (!string.Equals(status, "complete", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(payment, "paid", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = $"Session not completed/paid (status={status}, payment={payment})" });
    }

    // Перевірка власника
    if (!sess.Metadata.TryGetValue("userId", out var metaUser) ||
        !Guid.TryParse(metaUser, out var owner) ||
        owner != cur.Id)
    {
        return Results.Forbid();
    }

    var plan = (sess.Metadata.TryGetValue("plan", out var p) ? p : "pro")!
        .ToLowerInvariant();

    // ---- Обережно дістаємо період, інакше fallback +1 місяць ----
    DateTime? periodEnd = null;

    try
    {
        // у різних версіях є або SubscriptionId, або вже розгорнутий Subscription
        var subId = !string.IsNullOrWhiteSpace(sess.SubscriptionId)
            ? sess.SubscriptionId
            : sess.Subscription?.Id;

        if (!string.IsNullOrWhiteSpace(subId))
        {
            var subSvc = new Stripe.SubscriptionService();
            var sub = await subSvc.GetAsync(subId!, options: null, requestOptions: null, cancellationToken: ct);

            // без прив’язки до конкретного типу властивості
            var prop = sub.GetType().GetProperty("CurrentPeriodEnd");
            if (prop != null)
            {
                var val = prop.GetValue(sub);
                if (val is DateTime dt) periodEnd = dt.ToUniversalTime();
                else if (val is long unix) periodEnd = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }
        }
    }
    catch { /* ок для MVP */ }

    periodEnd ??= DateTime.UtcNow.AddMonths(1); // fallback

    var bill = await db.UserBillings.FindAsync(new object[] { cur.Id }, ct)
               ?? new UserBilling { UserId = cur.Id };

    bill.Plan = (plan == "business" || plan == "pro") ? plan : "pro";
    bill.CurrentPeriodEndUtc = periodEnd;

    db.UserBillings.Update(bill);
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { plan = bill.Plan, periodEnd = bill.CurrentPeriodEndUtc });
}).RequireAuthorization();



billing.MapGet("/success", () => Results.Ok(new { ok = true }));
billing.MapGet("/cancel", () => Results.Ok(new { canceled = true }));

app.Run();


record RegisterRequest(string Email, string Password, string confPassword);
record LoginRequest(string Email, string Password);
record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, string? UserId = null);
record CheckoutReq(string Plan);

