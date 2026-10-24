namespace Nudge.Api.Dtos;

public record CreateRoomRequest(string Name, List<int> MemberIds);
public record AddRoomMemberRequest(int UserId);
public record RoomResponse(int Id, string Name, int OwnerId, List<UserResponse> Members);
