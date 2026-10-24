using Nudge.Api.Dtos;
using Nudge.Api.Infrastructure;
using Nudge.Api.Services;

namespace Nudge.Api.Endpoints;

public static class MoodEndpoints
{
    public static void MapMoodEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/moods/presets", (IMoodService moods) => Results.Ok(moods.GetPresets()))
            .WithTags("Moods");

        var mood = app.MapGroup("/api/me/mood").WithTags("Moods").RequireAuthorization();

        // Preset or custom: both are just { emoji, text }.
        mood.MapPut("/", async (SetMoodRequest request, HttpContext http, IMoodService moods) =>
            Results.Ok(await moods.SetMoodAsync(http.User.GetUserId(), request)));

        mood.MapDelete("/", async (HttpContext http, IMoodService moods) =>
            Results.Ok(await moods.ClearMoodAsync(http.User.GetUserId())));
    }
}
