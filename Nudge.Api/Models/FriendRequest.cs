namespace Nudge.Api.Models;

// A pending request. Accepting it deletes the request and creates the friendship.
public class FriendRequest
{
    public int Id { get; set; }
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User FromUser { get; set; } = null!;
    public User ToUser { get; set; } = null!;
}
