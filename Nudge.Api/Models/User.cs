namespace Nudge.Api.Models;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
