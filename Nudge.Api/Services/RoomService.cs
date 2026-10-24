using Microsoft.EntityFrameworkCore;
using Nudge.Api.Data;
using Nudge.Api.Dtos;
using Nudge.Api.Infrastructure;
using Nudge.Api.Models;

namespace Nudge.Api.Services;

public interface IRoomService
{
    Task<List<RoomResponse>> GetRoomsAsync(int userId);
    Task<RoomResponse> GetRoomAsync(int userId, int roomId);
    Task<RoomResponse> CreateRoomAsync(int userId, CreateRoomRequest request);
    Task<RoomResponse> AddMemberAsync(int userId, int roomId, int newMemberId);
    Task RemoveMemberAsync(int userId, int roomId, int memberId);
    Task<bool> IsMemberAsync(int userId, int roomId);
    Task<List<int>> GetMemberIdsAsync(int roomId);
}

public class RoomService(NudgeDbContext db, IFriendService friends, INudgeNotifier notifier) : IRoomService
{
    public const int MaxNameLength = 40;

    public async Task<List<RoomResponse>> GetRoomsAsync(int userId)
    {
        var rooms = await RoomsWithMembers()
            .Where(r => r.Members.Any(m => m.UserId == userId))
            .OrderBy(r => r.Name)
            .ToListAsync();
        return rooms.Select(ToResponse).ToList();
    }

    public async Task<RoomResponse> GetRoomAsync(int userId, int roomId) =>
        ToResponse(await LoadForMemberAsync(userId, roomId));

    public async Task<RoomResponse> CreateRoomAsync(int userId, CreateRoomRequest request)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length is 0 or > MaxNameLength)
            throw new BadRequestException($"Room name must be 1-{MaxNameLength} characters.");

        var memberIds = (request.MemberIds ?? []).Where(id => id != userId).Distinct().ToList();
        foreach (var id in memberIds)
        {
            if (!await friends.AreFriendsAsync(userId, id))
                throw new BadRequestException("You can only add friends to a room.");
        }

        var room = new Room { Name = name, OwnerId = userId };
        room.Members.Add(new RoomMember { UserId = userId });
        room.Members.AddRange(memberIds.Select(id => new RoomMember { UserId = id }));

        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        await notifier.RoomsChangedAsync(room.Members.Select(m => m.UserId));

        return await GetRoomAsync(userId, room.Id);
    }

    public async Task<RoomResponse> AddMemberAsync(int userId, int roomId, int newMemberId)
    {
        var room = await LoadForMemberAsync(userId, roomId);

        // Any member can invite, but only their own friends. That keeps rooms to people who know each other.
        if (!await friends.AreFriendsAsync(userId, newMemberId))
            throw new BadRequestException("You can only add your own friends.");
        if (room.Members.Any(m => m.UserId == newMemberId))
            throw new ConflictException("They're already in this room.");

        db.RoomMembers.Add(new RoomMember { RoomId = roomId, UserId = newMemberId });
        await db.SaveChangesAsync();
        await notifier.RoomsChangedAsync(room.Members.Select(m => m.UserId).Append(newMemberId));

        return await GetRoomAsync(userId, roomId);
    }

    public async Task RemoveMemberAsync(int userId, int roomId, int memberId)
    {
        var room = await LoadForMemberAsync(userId, roomId);
        var leaving = memberId == userId;

        // You can always leave. Only the owner can remove someone else.
        if (!leaving && room.OwnerId != userId)
            throw new ForbiddenException("Only the room owner can remove members.");

        var member = room.Members.SingleOrDefault(m => m.UserId == memberId)
            ?? throw new NotFoundException("That user isn't in this room.");
        var notify = room.Members.Select(m => m.UserId).ToList();

        room.Members.Remove(member);
        if (room.Members.Count == 0)
        {
            db.Rooms.Remove(room);   // last person out deletes the room (and its nudges, via cascade)
        }
        else if (room.OwnerId == memberId)
        {
            // Hand ownership to whoever has been in the room longest.
            room.OwnerId = room.Members.OrderBy(m => m.JoinedAt).First().UserId;
        }

        await db.SaveChangesAsync();
        await notifier.RoomsChangedAsync(notify);
    }

    public Task<bool> IsMemberAsync(int userId, int roomId) =>
        db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == userId);

    public Task<List<int>> GetMemberIdsAsync(int roomId) =>
        db.RoomMembers.Where(m => m.RoomId == roomId).Select(m => m.UserId).ToListAsync();

    // Non-members get 404, not 403: a private room shouldn't even admit it exists.
    private async Task<Room> LoadForMemberAsync(int userId, int roomId) =>
        await RoomsWithMembers().SingleOrDefaultAsync(r => r.Id == roomId && r.Members.Any(m => m.UserId == userId))
        ?? throw new NotFoundException("Room not found.");

    private IQueryable<Room> RoomsWithMembers() =>
        db.Rooms.Include(r => r.Members).ThenInclude(m => m.User);

    private static RoomResponse ToResponse(Room r) =>
        new(r.Id, r.Name, r.OwnerId,
            r.Members.OrderBy(m => m.User.DisplayName).Select(m => m.User.ToResponse()).ToList());
}
