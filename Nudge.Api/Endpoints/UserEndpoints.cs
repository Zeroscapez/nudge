using Nudge.Api.Services;

namespace Nudge.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/users");   // every route below starts with /users

        group.MapGet("/{id:int}", async (int id, IUserService users) =>
            await users.GetAsync(id) is { } user ? Results.Ok(user) : Results.NotFound());

        group.MapPost("/", async (string name, IUserService users) =>
            await users.CreateAsync(name) is { } user ? Results.Ok(user) : Results.BadRequest());
        // TODO: group.MapPost("/", ...)                          create user
        // TODO: group.MapPost("/{id:int}/friends/{friendId:int}", ...)  add friend
        // TODO: group.MapGet("/{id:int}/friends", ...)            list friends
    }
}