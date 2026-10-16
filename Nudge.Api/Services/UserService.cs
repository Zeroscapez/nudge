using Nudge.Api.Dtos;
using Nudge.Api.Models;
using Nudge.Api.Services;

public class UserService : IUserService
{
    public Task<User> AddFriendAsync(int userId, int friendId)
    {
        throw new NotImplementedException();
    }

    public Task<User> CreateAsync(CreateUserRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<User> GetAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task<List<User>> GetFriendsAsync(int userId)
    {
        throw new NotImplementedException();
    }
}