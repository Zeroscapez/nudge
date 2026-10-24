using System.Net;
using System.Net.Http.Json;
using Nudge.Api.Dtos;

namespace Nudge.Api.Tests;

public class FriendAndMoodTests(NudgeApiFactory factory) : IClassFixture<NudgeApiFactory>
{
    [Fact]
    public async Task Request_then_accept_makes_both_users_friends()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var ana = await TestClient.RegisterAsync(factory);

        var sent = await zack.Http.PostAsJsonAsync("/api/friends/requests", new SendFriendRequestRequest(ana.User.Username));
        var result = await sent.Content.ReadFromJsonAsync<SendFriendRequestResult>();
        Assert.False(result!.BecameFriends);

        var requests = await ana.GetAsync<FriendRequestsResponse>("/api/friends/requests");
        var incoming = Assert.Single(requests.Incoming);
        Assert.Equal(zack.User.Id, incoming.From.Id);

        var accept = await ana.Http.PostAsync($"/api/friends/requests/{incoming.Id}/accept", null);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        Assert.Contains(await zack.GetAsync<List<UserResponse>>("/api/friends"), f => f.Id == ana.User.Id);
        Assert.Contains(await ana.GetAsync<List<UserResponse>>("/api/friends"), f => f.Id == zack.User.Id);
    }

    [Fact]
    public async Task Asking_someone_who_already_asked_you_makes_you_friends()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var ana = await TestClient.RegisterAsync(factory);

        await zack.Http.PostAsJsonAsync("/api/friends/requests", new SendFriendRequestRequest(ana.User.Username));
        var back = await ana.Http.PostAsJsonAsync("/api/friends/requests", new SendFriendRequestRequest(zack.User.Username));

        Assert.True((await back.Content.ReadFromJsonAsync<SendFriendRequestResult>())!.BecameFriends);
        Assert.Empty((await zack.GetAsync<FriendRequestsResponse>("/api/friends/requests")).Outgoing);
    }

    [Fact]
    public async Task You_cannot_friend_yourself_or_send_a_duplicate_request()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var ana = await TestClient.RegisterAsync(factory);

        var self = await zack.Http.PostAsJsonAsync("/api/friends/requests", new SendFriendRequestRequest(zack.User.Username));
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);

        await zack.Http.PostAsJsonAsync("/api/friends/requests", new SendFriendRequestRequest(ana.User.Username));
        var twice = await zack.Http.PostAsJsonAsync("/api/friends/requests", new SendFriendRequestRequest(ana.User.Username));
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
    }

    [Fact]
    public async Task Removing_a_friend_removes_both_directions()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var ana = await TestClient.RegisterAsync(factory);
        await zack.BefriendAsync(ana);

        var remove = await ana.Http.DeleteAsync($"/api/friends/{zack.User.Id}");

        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Empty(await zack.GetAsync<List<UserResponse>>("/api/friends"));
        Assert.Empty(await ana.GetAsync<List<UserResponse>>("/api/friends"));
    }

    [Fact]
    public async Task Friends_can_see_your_custom_mood()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var ana = await TestClient.RegisterAsync(factory);
        await zack.BefriendAsync(ana);

        var set = await ana.Http.PutAsJsonAsync("/api/me/mood", new SetMoodRequest("🎮", "Grinding ranked"));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        var friend = Assert.Single(await zack.GetAsync<List<UserResponse>>("/api/friends"));
        Assert.Equal(new MoodDto("🎮", "Grinding ranked"), friend.Mood);
    }

    [Theory]
    [InlineData("", "no emoji")]
    [InlineData("😀😀", "two emojis")]
    [InlineData("a", "a letter, not an emoji")]
    [InlineData("🙂", "this mood text is far too long to fit in thirty")]
    public async Task Invalid_moods_are_rejected(string emoji, string text)
    {
        var zack = await TestClient.RegisterAsync(factory);
        var response = await zack.Http.PutAsJsonAsync("/api/me/mood", new SetMoodRequest(emoji, text));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
