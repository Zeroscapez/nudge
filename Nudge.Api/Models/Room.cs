namespace Nudge.Api.Models;

// A private group. Only members can see it or nudge into it.
public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int OwnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RoomMember> Members { get; set; } = [];
}

public class RoomMember
{
    public int RoomId { get; set; }
    public int UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Room Room { get; set; } = null!;
    public User User { get; set; } = null!;
}
