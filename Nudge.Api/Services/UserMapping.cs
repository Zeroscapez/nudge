using Nudge.Api.Dtos;
using Nudge.Api.Models;

namespace Nudge.Api.Services;

// The single place an entity becomes a DTO, so every endpoint shows users the same way.
public static class UserMapping
{
    public static UserResponse ToResponse(this User u) =>
        new(u.Id, u.Username, u.DisplayName,
            u.MoodEmoji is null ? null : new MoodDto(u.MoodEmoji, u.MoodText ?? ""));
}
