using Microsoft.EntityFrameworkCore;
using Nudge.Api.Data;
using Nudge.Api.Dtos;
using Nudge.Api.Infrastructure;

namespace Nudge.Api.Services;

public interface IMoodService
{
    IReadOnlyList<MoodDto> GetPresets();
    Task<UserResponse> SetMoodAsync(int userId, SetMoodRequest request);
    Task<UserResponse> ClearMoodAsync(int userId);
}

public class MoodService(NudgeDbContext db, INudgeNotifier notifier, TimeProvider clock) : IMoodService
{
    public const int MaxMoodTextLength = 30;

    // Quick picks for the UI. Any emoji + text is allowed, so custom moods use the same endpoint.
    private static readonly MoodDto[] Presets =
    [
        new("😊", "Happy"),
        new("😴", "Sleepy"),
        new("🥰", "Loved up"),
        new("😢", "Sad"),
        new("😤", "Stressed"),
        new("🤒", "Sick"),
        new("🎮", "Gaming"),
        new("📚", "Busy"),
        new("🤗", "Need a hug"),
        new("🔕", "Do not disturb")
    ];

    public IReadOnlyList<MoodDto> GetPresets() => Presets;

    public async Task<UserResponse> SetMoodAsync(int userId, SetMoodRequest request)
    {
        var emoji = (request.Emoji ?? "").Trim();
        var text = (request.Text ?? "").Trim();

        if (EmojiValidator.CountEmojis(emoji) != 1 || !EmojiValidator.IsEmojiOnly(emoji))
            throw new BadRequestException("A mood needs exactly one emoji.");
        if (text.Length > MaxMoodTextLength)
            throw new BadRequestException($"Mood text can be at most {MaxMoodTextLength} characters.");

        var user = await db.Users.FindAsync(userId) ?? throw new NotFoundException("User not found.");
        user.MoodEmoji = emoji;
        user.MoodText = text;
        user.MoodUpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();

        return await BroadcastAsync(user);
    }

    public async Task<UserResponse> ClearMoodAsync(int userId)
    {
        var user = await db.Users.FindAsync(userId) ?? throw new NotFoundException("User not found.");
        user.MoodEmoji = null;
        user.MoodText = null;
        user.MoodUpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();

        return await BroadcastAsync(user);
    }

    // Friends see the new mood instantly, so they can check it before nudging.
    private async Task<UserResponse> BroadcastAsync(Models.User user)
    {
        var response = user.ToResponse();
        var friendIds = await db.Friendships.Where(f => f.UserId == user.Id).Select(f => f.FriendId).ToListAsync();
        await notifier.MoodChangedAsync(friendIds, response);
        return response;
    }
}
