using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Nudge.Api.Dtos;

namespace Nudge.Api.Hubs;

// The methods the server can call on a connected browser.
// A typed interface means a typo like "NudgeRecieved" is a compile error, not a silent bug.
public interface INudgeClient
{
    Task NudgeReceived(NudgeResponse nudge);
    Task MoodChanged(UserResponse user);
    Task FriendsChanged();
    Task RoomsChanged();
}

// Browsers connect here (wss://.../hubs/nudge) and stay connected,
// so the server can push a nudge the instant it's sent instead of the browser polling.
[Authorize]
public class NudgeHub : Hub<INudgeClient>;
