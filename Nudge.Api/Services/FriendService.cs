using Microsoft.EntityFrameworkCore;
using Nudge.Api.Data;
using Nudge.Api.Dtos;
using Nudge.Api.Infrastructure;
using Nudge.Api.Models;

namespace Nudge.Api.Services;

public interface IFriendService
{
    Task<List<UserResponse>> GetFriendsAsync(int userId);
    Task<bool> AreFriendsAsync(int userId, int otherId);
    Task<SendFriendRequestResult> SendRequestAsync(int userId, string username);
    Task<FriendRequestsResponse> GetRequestsAsync(int userId);
    Task AcceptRequestAsync(int userId, int requestId);
    Task DeleteRequestAsync(int userId, int requestId);
    Task RemoveFriendAsync(int userId, int friendId);
}

public class FriendService(NudgeDbContext db, INudgeNotifier notifier) : IFriendService
{
    public async Task<List<UserResponse>> GetFriendsAsync(int userId)
    {
        var friends = await db.Friendships
            .Where(f => f.UserId == userId)
            .Select(f => f.Friend)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        return friends.Select(u => u.ToResponse()).ToList();
    }

    public Task<bool> AreFriendsAsync(int userId, int otherId) =>
        db.Friendships.AnyAsync(f => f.UserId == userId && f.FriendId == otherId);

    public async Task<SendFriendRequestResult> SendRequestAsync(int userId, string username)
    {
        var normalized = AuthService.NormalizeUsername(username);
        var target = await db.Users.SingleOrDefaultAsync(u => u.Username == normalized)
            ?? throw new NotFoundException($"No user called '{normalized}'.");

        if (target.Id == userId)
            throw new BadRequestException("You can't add yourself as a friend.");
        if (await AreFriendsAsync(userId, target.Id))
            throw new ConflictException("You're already friends.");
        if (await db.FriendRequests.AnyAsync(r => r.FromUserId == userId && r.ToUserId == target.Id))
            throw new ConflictException("You already sent them a request.");

        // They already asked us: treat our request as accepting theirs.
        var theirs = await db.FriendRequests.SingleOrDefaultAsync(r => r.FromUserId == target.Id && r.ToUserId == userId);
        if (theirs is not null)
        {
            await AcceptAsync(theirs);
            return new SendFriendRequestResult(true, null);
        }

        var request = new FriendRequest { FromUserId = userId, ToUserId = target.Id };
        db.FriendRequests.Add(request);
        await db.SaveChangesAsync();
        await notifier.FriendsChangedAsync([target.Id]);

        var loaded = await LoadRequests().SingleAsync(r => r.Id == request.Id);
        return new SendFriendRequestResult(false, ToResponse(loaded));
    }

    public async Task<FriendRequestsResponse> GetRequestsAsync(int userId)
    {
        var requests = await LoadRequests()
            .Where(r => r.ToUserId == userId || r.FromUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return new FriendRequestsResponse(
            requests.Where(r => r.ToUserId == userId).Select(ToResponse).ToList(),
            requests.Where(r => r.FromUserId == userId).Select(ToResponse).ToList());
    }

    public async Task AcceptRequestAsync(int userId, int requestId)
    {
        // Only the person the request was sent TO can accept it.
        var request = await db.FriendRequests.SingleOrDefaultAsync(r => r.Id == requestId && r.ToUserId == userId)
            ?? throw new NotFoundException("Friend request not found.");
        await AcceptAsync(request);
    }

    public async Task DeleteRequestAsync(int userId, int requestId)
    {
        // The receiver can decline it, and the sender can cancel it.
        var request = await db.FriendRequests
            .SingleOrDefaultAsync(r => r.Id == requestId && (r.ToUserId == userId || r.FromUserId == userId))
            ?? throw new NotFoundException("Friend request not found.");

        db.FriendRequests.Remove(request);
        await db.SaveChangesAsync();
        await notifier.FriendsChangedAsync([request.FromUserId, request.ToUserId]);
    }

    public async Task RemoveFriendAsync(int userId, int friendId)
    {
        var rows = await db.Friendships
            .Where(f => (f.UserId == userId && f.FriendId == friendId) || (f.UserId == friendId && f.FriendId == userId))
            .ToListAsync();
        if (rows.Count == 0)
            throw new NotFoundException("You're not friends with that user.");

        db.Friendships.RemoveRange(rows);
        await db.SaveChangesAsync();
        await notifier.FriendsChangedAsync([userId, friendId]);
    }

    private async Task AcceptAsync(FriendRequest request)
    {
        db.FriendRequests.Remove(request);
        db.Friendships.Add(new Friendship { UserId = request.FromUserId, FriendId = request.ToUserId });
        db.Friendships.Add(new Friendship { UserId = request.ToUserId, FriendId = request.FromUserId });
        await db.SaveChangesAsync();   // one SaveChanges = one transaction: all three changes happen, or none do
        await notifier.FriendsChangedAsync([request.FromUserId, request.ToUserId]);
    }

    private IQueryable<FriendRequest> LoadRequests() =>
        db.FriendRequests.Include(r => r.FromUser).Include(r => r.ToUser);

    private static FriendRequestResponse ToResponse(FriendRequest r) =>
        new(r.Id, r.FromUser.ToResponse(), r.ToUser.ToResponse(), r.CreatedAt);
}
