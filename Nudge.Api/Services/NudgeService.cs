using Nudge.Api.Models;

namespace Nudge.Api.Services;

public class NudgeService : INudgeService
{
    public Task<NudgeMessage> SendAsync(int fromUserId, int toUserId, string? message)
    {
        throw new NotImplementedException();
    }
}