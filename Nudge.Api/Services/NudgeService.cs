using Microsoft.EntityFrameworkCore;
using Nudge.Api.Data;
using Nudge.Api.Dtos;
using Nudge.Api.Infrastructure;
using Nudge.Api.Models;

namespace Nudge.Api.Services;

public interface INudgeService
{
    Task<NudgeResponse> SendAsync(int fromUserId, SendNudgeRequest request);
    Task<List<NudgeResponse>> GetInboxAsync(int userId, int limit = 50);
    Task<List<NudgeResponse>> GetRoomNudgesAsync(int userId, int roomId, int limit = 50);
    Task MarkReadAsync(int userId, int nudgeId);
}

public class NudgeService(
    NudgeDbContext db,
    IFriendService friends,
    IRoomService rooms,
    INudgeNotifier notifier,
    TimeProvider clock) : INudgeService
{
    public async Task<NudgeResponse> SendAsync(int fromUserId, SendNudgeRequest request)
    {
        if (request.ToUserId.HasValue == request.RoomId.HasValue)
            throw new BadRequestException("Send a nudge to either one friend (toUserId) or one room (roomId).");

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > NudgeMessage.MaxNoteLength })
            throw new BadRequestException($"Notes can be at most {NudgeMessage.MaxNoteLength} characters.");

        string? emojis = string.Concat((request.Emojis ?? "").Where(c => !char.IsWhiteSpace(c)));
        if (emojis.Length == 0) emojis = null;
        if (emojis is not null)
        {
            if (EmojiValidator.CountEmojis(emojis) > NudgeMessage.MaxEmojis)
                throw new BadRequestException($"You can attach at most {NudgeMessage.MaxEmojis} emojis.");
            if (!EmojiValidator.IsEmojiOnly(emojis))
                throw new BadRequestException("Only emojis can go in the emoji slot. Put words in the note.");
        }

        List<int> recipients;
        if (request.ToUserId is int toUserId)
        {
            if (toUserId == fromUserId)
                throw new BadRequestException("You can't nudge yourself.");
            if (!await friends.AreFriendsAsync(fromUserId, toUserId))
                throw new ForbiddenException("You can only nudge your friends.");
            recipients = [toUserId];
        }
        else
        {
            var roomId = request.RoomId!.Value;
            if (!await rooms.IsMemberAsync(fromUserId, roomId))
                throw new NotFoundException("Room not found.");
            recipients = (await rooms.GetMemberIdsAsync(roomId)).Where(id => id != fromUserId).ToList();
        }

        var nudge = new NudgeMessage
        {
            FromUserId = fromUserId,
            ToUserId = request.ToUserId,
            RoomId = request.RoomId,
            Note = note,
            Emojis = emojis,
            SentAt = clock.GetUtcNow().UtcDateTime
        };
        db.Nudges.Add(nudge);
        await db.SaveChangesAsync();

        var response = await Project(db.Nudges.Where(n => n.Id == nudge.Id)).SingleAsync();
        await notifier.NudgeReceivedAsync(recipients, response);
        return response;
    }

    public async Task<List<NudgeResponse>> GetInboxAsync(int userId, int limit = 50) =>
        await Project(db.Nudges
                .Where(n => n.ToUserId == userId)
                .OrderByDescending(n => n.SentAt)
                .Take(Math.Clamp(limit, 1, 100)))
            .ToListAsync();

    public async Task<List<NudgeResponse>> GetRoomNudgesAsync(int userId, int roomId, int limit = 50)
    {
        if (!await rooms.IsMemberAsync(userId, roomId))
            throw new NotFoundException("Room not found.");

        return await Project(db.Nudges
                .Where(n => n.RoomId == roomId)
                .OrderByDescending(n => n.SentAt)
                .Take(Math.Clamp(limit, 1, 100)))
            .ToListAsync();
    }

    public async Task MarkReadAsync(int userId, int nudgeId)
    {
        var nudge = await db.Nudges.SingleOrDefaultAsync(n => n.Id == nudgeId && n.ToUserId == userId)
            ?? throw new NotFoundException("Nudge not found.");
        nudge.ReadAt ??= clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
    }

    // Builds the DTO inside the database query, so only the needed columns are read.
    private IQueryable<NudgeResponse> Project(IQueryable<NudgeMessage> query) =>
        query.Select(n => new NudgeResponse(
            n.Id,
            new UserResponse(n.FromUser.Id, n.FromUser.Username, n.FromUser.DisplayName,
                n.FromUser.MoodEmoji == null ? null : new MoodDto(n.FromUser.MoodEmoji, n.FromUser.MoodText ?? "")),
            n.ToUserId,
            n.RoomId,
            db.Rooms.Where(r => r.Id == n.RoomId).Select(r => r.Name).FirstOrDefault(),
            n.Note,
            n.Emojis,
            n.SentAt,
            n.ReadAt));
}
