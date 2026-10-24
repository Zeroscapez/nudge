using Microsoft.AspNetCore.SignalR;
using Nudge.Api.Dtos;
using Nudge.Api.Hubs;

namespace Nudge.Api.Services;

// Services depend on this interface, not on SignalR directly.
// That keeps them testable and lets you swap in push notifications later without touching them.
public interface INudgeNotifier
{
    Task NudgeReceivedAsync(IEnumerable<int> recipientIds, NudgeResponse nudge);
    Task MoodChangedAsync(IEnumerable<int> friendIds, UserResponse user);
    Task FriendsChangedAsync(IEnumerable<int> userIds);
    Task RoomsChangedAsync(IEnumerable<int> userIds);
}

public class SignalRNudgeNotifier(IHubContext<NudgeHub, INudgeClient> hub) : INudgeNotifier
{
    public Task NudgeReceivedAsync(IEnumerable<int> recipientIds, NudgeResponse nudge) =>
        hub.Clients.Users(Ids(recipientIds)).NudgeReceived(nudge);

    public Task MoodChangedAsync(IEnumerable<int> friendIds, UserResponse user) =>
        hub.Clients.Users(Ids(friendIds)).MoodChanged(user);

    public Task FriendsChangedAsync(IEnumerable<int> userIds) =>
        hub.Clients.Users(Ids(userIds)).FriendsChanged();

    public Task RoomsChangedAsync(IEnumerable<int> userIds) =>
        hub.Clients.Users(Ids(userIds)).RoomsChanged();

    private static List<string> Ids(IEnumerable<int> ids) => ids.Select(i => i.ToString()).ToList();
}
