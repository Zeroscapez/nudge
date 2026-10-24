using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nudge.Api.Dtos;

namespace Nudge.Api.Tests;

// One logged-in user. Wraps HttpClient so the tests read like a story.
public class TestClient(HttpClient http, UserResponse user, string token)
{
    public HttpClient Http { get; } = http;
    public UserResponse User { get; } = user;
    public string Token { get; } = token;

    private static int _counter;

    public static async Task<TestClient> RegisterAsync(NudgeApiFactory factory, string? username = null)
    {
        username ??= $"user{Interlocked.Increment(ref _counter)}";
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(username, username.ToUpperInvariant(), "password123"));
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return new TestClient(http, auth.User, auth.Token);
    }

    public async Task BefriendAsync(TestClient other)
    {
        (await Http.PostAsJsonAsync("/api/friends/requests", new SendFriendRequestRequest(other.User.Username))).EnsureSuccessStatusCode();
        // Sending one back accepts the first request.
        var result = await other.Http.PostAsJsonAsync("/api/friends/requests", new SendFriendRequestRequest(User.Username));
        result.EnsureSuccessStatusCode();
    }

    public Task<HttpResponseMessage> NudgeAsync(int? toUserId = null, int? roomId = null, string? note = null, string? emojis = null) =>
        Http.PostAsJsonAsync("/api/nudges", new SendNudgeRequest(toUserId, roomId, note, emojis));

    public async Task<T> GetAsync<T>(string url) => (await Http.GetFromJsonAsync<T>(url))!;
}
