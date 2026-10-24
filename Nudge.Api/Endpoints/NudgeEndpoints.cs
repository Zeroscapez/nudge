using Nudge.Api.Dtos;
using Nudge.Api.Infrastructure;
using Nudge.Api.Services;

namespace Nudge.Api.Endpoints;

public static class NudgeEndpoints
{
    public static void MapNudgeEndpoints(this IEndpointRouteBuilder app)
    {
        var nudges = app.MapGroup("/api/nudges").WithTags("Nudges").RequireAuthorization();

        nudges.MapPost("/", async (SendNudgeRequest request, HttpContext http, INudgeService service) =>
            {
                var nudge = await service.SendAsync(http.User.GetUserId(), request);
                return Results.Created($"/api/nudges/{nudge.Id}", nudge);
            })
            .RequireRateLimiting("nudges");

        nudges.MapGet("/inbox", async (HttpContext http, INudgeService service) =>
            Results.Ok(await service.GetInboxAsync(http.User.GetUserId())));

        nudges.MapPost("/{id:int}/read", async (int id, HttpContext http, INudgeService service) =>
        {
            await service.MarkReadAsync(http.User.GetUserId(), id);
            return Results.NoContent();
        });
    }
}
