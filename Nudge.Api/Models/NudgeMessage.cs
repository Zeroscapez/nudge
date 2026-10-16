using System.ComponentModel.DataAnnotations;

namespace Nudge.Api.Models;

public class NudgeMessage
{
    int Id { get; set; }
    int FromUserId { get; set; }
    int ToUserId { get; set; }

    [MaxLength(140)]
    string? Message { get; set; }

    DateTime SentAt { get; set; }

    DateTime? ReadAt { get; set; }
}