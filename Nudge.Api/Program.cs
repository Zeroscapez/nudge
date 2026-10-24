using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nudge.Api.Data;
using Nudge.Api.Endpoints;
using Nudge.Api.Hubs;
using Nudge.Api.Infrastructure;
using Nudge.Api.Models;
using Nudge.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuration ----------
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key must be at least 32 characters. Set it with the Jwt__Key environment variable.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// ---------- Database ----------
builder.Services.AddDbContext<NudgeDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Nudge") ?? "Data Source=nudge.db"));

// ---------- Authentication (who are you?) ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;   // keep claim names as written in the token ("sub", not a long URI)
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Browsers can't set headers on a WebSocket, so the SignalR client sends the token
        // in the query string instead. Only accept that for the hub path.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();   // (what are you allowed to do?)

// ---------- Rate limiting: slows down password guessing and nudge spam ----------
var authPerMinute = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 20);
var nudgesPerMinute = builder.Configuration.GetValue("RateLimits:NudgesPerMinute", 30);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("nudges", http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.Identity?.Name ?? http.User.FindFirst("sub")?.Value ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = nudgesPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

// ---------- Real-time ----------
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();

// ---------- Errors ----------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

// ---------- Our services ----------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<IMoodService, MoodService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<INudgeService, NudgeService>();
builder.Services.AddScoped<INudgeNotifier, SignalRNudgeNotifier>();

var app = builder.Build();

// Create or upgrade the database to the latest migration on startup.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<NudgeDbContext>().Database.Migrate();
}

// ---------- Request pipeline (order matters) ----------
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();   // "/" serves wwwroot/index.html
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapMoodEndpoints();
app.MapFriendEndpoints();
app.MapRoomEndpoints();
app.MapNudgeEndpoints();
app.MapHub<NudgeHub>("/hubs/nudge");

app.Run();

// Lets the test project start the app with WebApplicationFactory<Program>.
public partial class Program;
