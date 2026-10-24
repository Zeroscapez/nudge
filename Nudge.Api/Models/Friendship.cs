namespace Nudge.Api.Models;

// An accepted friendship, stored as two rows (A→B and B→A)
// so "who are X's friends?" is always a simple UserId == X lookup.
public class Friendship
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FriendId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Friend { get; set; } = null!;
}
