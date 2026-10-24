namespace Nudge.Api.Dtos;

public record SendFriendRequestRequest(string Username);
public record FriendRequestResponse(int Id, UserResponse From, UserResponse To, DateTime CreatedAt);
public record FriendRequestsResponse(List<FriendRequestResponse> Incoming, List<FriendRequestResponse> Outgoing);

// Sending a request to someone who already asked you makes you friends straight away.
public record SendFriendRequestResult(bool BecameFriends, FriendRequestResponse? Request);
