using Nudge.Api.Models;
using Nudge.Api.Data;      // only needed in the classes that use db

namespace Nudge.Api.Services;

public interface INudgeService
{
    Task<NudgeMessage> SendAsync(int fromUserId, int toUserId, string? message);
}