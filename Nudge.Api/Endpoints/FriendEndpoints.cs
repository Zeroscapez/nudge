using Nudge.Api.Dtos;
using Nudge.Api.Infrastructure;
using Nudge.Api.Services;

namespace Nudge.Api.Endpoints;

public static class FriendEndpoints
{
    public static void MapFriendEndpoints(this IEndpointRouteBuilder app)
    {
        var friends = app.MapGroup("/api/friends").WithTags("Friends").RequireAuthorization();

        friends.MapGet("/", async (HttpContext http, IFriendService service) =>
            Results.Ok(await service.GetFriendsAsync(http.User.GetUserId())));

        friends.MapDelete("/{friendId:int}", async (int friendId, HttpContext http, IFriendService service) =>
        {
            await service.RemoveFriendAsync(http.User.GetUserId(), friendId);
            return Results.NoContent();
        });

        var requests = friends.MapGroup("/requests");

        requests.MapGet("/", async (HttpContext http, IFriendService service) =>
            Results.Ok(await service.GetRequestsAsync(http.User.GetUserId())));

        requests.MapPost("/", async (SendFriendRequestRequest request, HttpContext http, IFriendService service) =>
            Results.Ok(await service.SendRequestAsync(http.User.GetUserId(), request.Username)));

        requests.MapPost("/{id:int}/accept", async (int id, HttpContext http, IFriendService service) =>
        {
            await service.AcceptRequestAsync(http.User.GetUserId(), id);
            return Results.NoContent();
        });

        // Declines (if it was sent to you) or cancels (if you sent it).
        requests.MapDelete("/{id:int}", async (int id, HttpContext http, IFriendService service) =>
        {
            await service.DeleteRequestAsync(http.User.GetUserId(), id);
            return Results.NoContent();
        });
    }
}
