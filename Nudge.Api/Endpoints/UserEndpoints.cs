using Nudge.Api.Services;
using Nudge.Api.Dtos;

namespace Nudge.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/users");   // every route below starts with /users

        group.MapGet("/{id:int}", async (int id, IUserService users) =>
            await users.GetAsync(id) is { } user ? Results.Ok(user) : Results.NotFound());

        group.MapPost("/", async (CreateUserRequest request, IUserService users) =>
            await users.CreateAsync(request) is { } user ? Results.Created($"/users/{user.Id}", user) : Results.BadRequest($"Username '{request.Username}' is already taken."));
        // TODO: group.MapPost("/", ...)                          create user
        // TODO: group.MapPost("/{id:int}/friends/{friendId:int}", ...)  add friend
        // TODO: group.MapGet("/{id:int}/friends", ...)            list friends
    }
}