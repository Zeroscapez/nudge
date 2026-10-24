using Nudge.Api.Dtos;
using Nudge.Api.Infrastructure;
using Nudge.Api.Services;

namespace Nudge.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        // The only two endpoints you can call without a token.
        auth.MapPost("/register", async (RegisterRequest request, IAuthService service) =>
            Results.Created("/api/me", await service.RegisterAsync(request)))
            .RequireRateLimiting("auth");

        auth.MapPost("/login", async (LoginRequest request, IAuthService service) =>
            Results.Ok(await service.LoginAsync(request)))
            .RequireRateLimiting("auth");

        var me = app.MapGroup("/api/me").WithTags("Me").RequireAuthorization();

        me.MapGet("/", async (HttpContext http, IAuthService service) =>
            Results.Ok(await service.GetMeAsync(http.User.GetUserId())));
    }
}
