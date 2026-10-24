namespace Nudge.Api.Dtos;

// Set exactly one of ToUserId or RoomId.
public record SendNudgeRequest(int? ToUserId, int? RoomId, string? Note, string? Emojis);

public record NudgeResponse(
    int Id,
    UserResponse From,
    int? ToUserId,
    int? RoomId,
    string? RoomName,
    string? Note,
    string? Emojis,
    DateTime SentAt,
    DateTime? ReadAt);
