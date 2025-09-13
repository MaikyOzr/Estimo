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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddHealthChecks();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();

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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EstimoDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
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

app.MapHealthChecks("/health");
app.UseCors();
var clientsMap = app.MapGroup("/clients").RequireAuthorization();
var quoteMap = app.MapGroup("/quotes").RequireAuthorization();
var authMap = app.MapGroup("/auth").AllowAnonymous();


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

quoteMap.MapGet("/{id:guid}", async(Guid id, GetQuoteByIdQuery query) => {
    var quote = await query.Handle(id, CancellationToken.None);
    return Results.Ok(quote);
});

quoteMap.MapGet("/{id:guid}/pdf", async (Guid id, QuotePdfService service, CancellationToken ct) =>
{
    var pdf = await service.GeneratePdfAsync(id, ct);
    return Results.File(pdf, "application/pdf", $"quote-{id.ToString()[..8]}.pdf");
});


app.Run();


record RegisterRequest(string Email, string Password, string confPassword);
record LoginRequest(string Email, string Password);
record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, string? UserId = null);