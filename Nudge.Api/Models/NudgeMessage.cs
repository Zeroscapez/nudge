namespace Nudge.Api.Models;

// A nudge goes either to one friend (ToUserId) or to a room (RoomId), never both.
public class NudgeMessage
{
    public const int MaxNoteLength = 140;
    public const int MaxEmojis = 2;

    public int Id { get; set; }
    public int FromUserId { get; set; }
    public int? ToUserId { get; set; }
    public int? RoomId { get; set; }

    public string? Note { get; set; }
    public string? Emojis { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public User FromUser { get; set; } = null!;
}
