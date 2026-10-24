namespace Nudge.Api.Dtos;

public record MoodDto(string Emoji, string Text);

// What the API shows about any user. Never includes the password hash.
public record UserResponse(int Id, string Username, string DisplayName, MoodDto? Mood);

public record SetMoodRequest(string Emoji, string Text);
