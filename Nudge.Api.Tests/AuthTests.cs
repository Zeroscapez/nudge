using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nudge.Api.Data;
using Nudge.Api.Dtos;

namespace Nudge.Api.Tests;

public class AuthTests(NudgeApiFactory factory) : IClassFixture<NudgeApiFactory>
{
    [Fact]
    public async Task Register_then_login_returns_a_working_token()
    {
        var http = factory.CreateClient();
        var register = await http.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Zack_1", "Zack", "correct-horse"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        // Usernames are case-insensitive.
        var login = await http.PostAsJsonAsync("/api/auth/login", new LoginRequest("ZACK_1", "correct-horse"));
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("zack_1", auth!.User.Username);

        http.DefaultRequestHeaders.Authorization = new("Bearer", auth.Token);
        var me = await http.GetFromJsonAsync<UserResponse>("/api/me");
        Assert.Equal("Zack", me!.DisplayName);
    }

    [Fact]
    public async Task Passwords_are_stored_hashed_not_in_plain_text()
    {
        await TestClient.RegisterAsync(factory, "hashcheck");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NudgeDbContext>();
        var user = await db.Users.SingleAsync(u => u.Username == "hashcheck");

        Assert.DoesNotContain("password123", user.PasswordHash);
        Assert.True(user.PasswordHash.Length > 40);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_user_give_the_same_401()
    {
        await TestClient.RegisterAsync(factory, "samefail");
        var http = factory.CreateClient();

        var wrongPassword = await http.PostAsJsonAsync("/api/auth/login", new LoginRequest("samefail", "not-the-password"));
        var unknownUser = await http.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody_here", "not-the-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        var a = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        var b = await unknownUser.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(a!.Detail, b!.Detail);
    }

    [Fact]
    public async Task Duplicate_username_is_a_409()
    {
        await TestClient.RegisterAsync(factory, "taken");
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest("TAKEN", "x", "password123"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("ab", "password123")]          // username too short
    [InlineData("has space", "password123")]   // invalid characters
    [InlineData("validname", "short")]         // password too short
    public async Task Bad_registration_input_is_a_400(string username, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, "x", password));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_need_a_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/friends");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
