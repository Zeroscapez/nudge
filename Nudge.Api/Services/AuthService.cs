using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nudge.Api.Data;
using Nudge.Api.Dtos;
using Nudge.Api.Infrastructure;
using Nudge.Api.Models;

namespace Nudge.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserResponse> GetMeAsync(int userId);
}

public partial class AuthService(
    NudgeDbContext db,
    IPasswordHasher<User> hasher,
    ITokenService tokens) : IAuthService
{
    public const int MinPasswordLength = 8;

    [GeneratedRegex("^[a-z0-9_]{3,20}$")]
    private static partial Regex UsernamePattern();

    public static string NormalizeUsername(string? username) =>
        (username ?? "").Trim().ToLowerInvariant();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var username = NormalizeUsername(request.Username);
        if (!UsernamePattern().IsMatch(username))
            throw new BadRequestException("Username must be 3-20 characters: letters, numbers or _.");

        var password = request.Password ?? "";
        if (password.Length < MinPasswordLength)
            throw new BadRequestException($"Password must be at least {MinPasswordLength} characters.");

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim();
        if (displayName.Length > 40)
            throw new BadRequestException("Display name can be at most 40 characters.");

        if (await db.Users.AnyAsync(u => u.Username == username))
            throw new ConflictException("That username is taken.");

        var user = new User { Username = username, DisplayName = displayName };
        // PBKDF2 with a random salt and many iterations: slow on purpose, so stolen hashes are hard to crack.
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var username = NormalizeUsername(request.Username);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username);

        // Same message whether the username or the password is wrong,
        // so attackers can't use the login form to find out which usernames exist.
        const string failure = "Wrong username or password.";
        if (user is null)
            throw new UnauthorizedException(failure);

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? "");
        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedException(failure);

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // The hashing settings got stronger since this hash was made. Upgrade it now that we know the password.
            user.PasswordHash = hasher.HashPassword(user, request.Password!);
            await db.SaveChangesAsync();
        }

        return CreateAuthResponse(user);
    }

    public async Task<UserResponse> GetMeAsync(int userId)
    {
        var user = await db.Users.FindAsync(userId) ?? throw new NotFoundException("User not found.");
        return user.ToResponse();
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var (token, expires) = tokens.CreateToken(user);
        return new AuthResponse(token, expires, user.ToResponse());
    }
}
