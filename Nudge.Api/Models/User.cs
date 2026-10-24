namespace Nudge.Api.Models;

public class User
{
    public int Id { get; set; }

    // Stored lowercase so "Zack" and "zack" can't both exist.
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";

    // Never the password itself: a salted PBKDF2 hash produced by PasswordHasher.
    public string PasswordHash { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Current mood. Null means "no mood set".
    public string? MoodEmoji { get; set; }
    public string? MoodText { get; set; }
    public DateTime? MoodUpdatedAt { get; set; }
}
