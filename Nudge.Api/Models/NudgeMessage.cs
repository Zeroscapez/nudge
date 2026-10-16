using System.ComponentModel.DataAnnotations;

namespace Nudge.Api.Models;

public class NudgeMessage
{
    public int Id { get; set; }
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }

    [MaxLength(140)]
    public string? Message { get; set; }

    public DateTime SentAt { get; set; }

    public DateTime? ReadAt { get; set; }
}