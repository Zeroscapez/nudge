using Nudge.Api.Dtos;
using Nudge.Api.Infrastructure;
using Nudge.Api.Services;

namespace Nudge.Api.Endpoints;

public static class RoomEndpoints
{
    public static void MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var rooms = app.MapGroup("/api/rooms").WithTags("Rooms").RequireAuthorization();

        rooms.MapGet("/", async (HttpContext http, IRoomService service) =>
            Results.Ok(await service.GetRoomsAsync(http.User.GetUserId())));

        rooms.MapPost("/", async (CreateRoomRequest request, HttpContext http, IRoomService service) =>
        {
            var room = await service.CreateRoomAsync(http.User.GetUserId(), request);
            return Results.Created($"/api/rooms/{room.Id}", room);
        });

        rooms.MapGet("/{id:int}", async (int id, HttpContext http, IRoomService service) =>
            Results.Ok(await service.GetRoomAsync(http.User.GetUserId(), id)));

        rooms.MapPost("/{id:int}/members", async (int id, AddRoomMemberRequest request, HttpContext http, IRoomService service) =>
            Results.Ok(await service.AddMemberAsync(http.User.GetUserId(), id, request.UserId)));

        // DELETE your own id to leave; the owner can DELETE anyone else's.
        rooms.MapDelete("/{id:int}/members/{userId:int}", async (int id, int userId, HttpContext http, IRoomService service) =>
        {
            await service.RemoveMemberAsync(http.User.GetUserId(), id, userId);
            return Results.NoContent();
        });

        rooms.MapGet("/{id:int}/nudges", async (int id, HttpContext http, INudgeService nudges) =>
            Results.Ok(await nudges.GetRoomNudgesAsync(http.User.GetUserId(), id)));
    }
}
