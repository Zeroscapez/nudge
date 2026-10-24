using System.Net;
using System.Net.Http.Json;
using Nudge.Api.Dtos;

namespace Nudge.Api.Tests;

public class NudgeAndRoomTests(NudgeApiFactory factory) : IClassFixture<NudgeApiFactory>
{
    [Fact]
    public async Task A_nudge_with_a_note_and_two_emojis_lands_in_the_inbox()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var ana = await TestClient.RegisterAsync(factory);
        await zack.BefriendAsync(ana);

        // 👍🏽 is two code points but one emoji, and ❤️ carries an invisible variation selector.
        var sent = await zack.NudgeAsync(toUserId: ana.User.Id, note: "thinking of you", emojis: "👍🏽❤️");
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);

        var nudge = Assert.Single(await ana.GetAsync<List<NudgeResponse>>("/api/nudges/inbox"));
        Assert.Equal("thinking of you", nudge.Note);
        Assert.Equal("👍🏽❤️", nudge.Emojis);
        Assert.Equal(zack.User.Id, nudge.From.Id);
        Assert.Null(nudge.ReadAt);

        var read = await ana.Http.PostAsync($"/api/nudges/{nudge.Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.NotNull(Assert.Single(await ana.GetAsync<List<NudgeResponse>>("/api/nudges/inbox")).ReadAt);
    }

    [Fact]
    public async Task You_can_only_nudge_friends()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var stranger = await TestClient.RegisterAsync(factory);

        var response = await zack.NudgeAsync(toUserId: stranger.User.Id);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "🔥🔥🔥")]      // three emojis
    [InlineData(null, "hi")]          // words in the emoji slot
    [InlineData("x", null, 141)]      // note too long
    public async Task Invalid_nudges_are_a_400(string? note, string? emojis, int noteLength = 0)
    {
        var zack = await TestClient.RegisterAsync(factory);
        var ana = await TestClient.RegisterAsync(factory);
        await zack.BefriendAsync(ana);

        if (noteLength > 0) note = new string('x', noteLength);
        var response = await zack.NudgeAsync(toUserId: ana.User.Id, note: note, emojis: emojis);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_nudge_needs_exactly_one_target()
    {
        var zack = await TestClient.RegisterAsync(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await zack.NudgeAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await zack.NudgeAsync(toUserId: 1, roomId: 1)).StatusCode);
    }

    [Fact]
    public async Task Room_nudges_go_to_the_room_feed_and_outsiders_get_404()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var ana = await TestClient.RegisterAsync(factory);
        var bo = await TestClient.RegisterAsync(factory);
        var outsider = await TestClient.RegisterAsync(factory);
        await zack.BefriendAsync(ana);
        await zack.BefriendAsync(bo);

        var created = await zack.Http.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("Trio", [ana.User.Id, bo.User.Id]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var room = (await created.Content.ReadFromJsonAsync<RoomResponse>())!;
        Assert.Equal(3, room.Members.Count);

        Assert.Equal(HttpStatusCode.Created, (await ana.NudgeAsync(roomId: room.Id, emojis: "🎉")).StatusCode);

        // Room nudges show up in the room's feed for every member; the inbox is for direct nudges.
        var feed = await bo.GetAsync<List<NudgeResponse>>($"/api/rooms/{room.Id}/nudges");
        Assert.Equal("🎉", Assert.Single(feed).Emojis);
        Assert.Empty(await bo.GetAsync<List<NudgeResponse>>("/api/nudges/inbox"));

        // Outsiders can't even tell the room exists.
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Http.GetAsync($"/api/rooms/{room.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.NudgeAsync(roomId: room.Id)).StatusCode);
    }

    [Fact]
    public async Task Only_friends_can_be_added_to_a_room()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var stranger = await TestClient.RegisterAsync(factory);

        var response = await zack.Http.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("Nope", [stranger.User.Id]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task When_the_owner_leaves_the_next_member_takes_over()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var ana = await TestClient.RegisterAsync(factory);
        await zack.BefriendAsync(ana);
        var room = (await (await zack.Http.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("Duo", [ana.User.Id])))
            .Content.ReadFromJsonAsync<RoomResponse>())!;

        // Ana isn't the owner, so she can't kick Zack.
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.Http.DeleteAsync($"/api/rooms/{room.Id}/members/{zack.User.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await zack.Http.DeleteAsync($"/api/rooms/{room.Id}/members/{zack.User.Id}")).StatusCode);

        var after = await ana.GetAsync<RoomResponse>($"/api/rooms/{room.Id}");
        Assert.Equal(ana.User.Id, after.OwnerId);
        Assert.Single(after.Members);
    }
}
