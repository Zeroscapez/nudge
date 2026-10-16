using Nudge.Api.Models;
using Nudge.Api.Data;
using Nudge.Api.Dtos;      // only needed in the classes that use db

namespace Nudge.Api.Services;

public interface IUserService
{
    Task<User> CreateAsync(CreateUserRequest request);
    Task<User> GetAsync(int id);

    Task<User> AddFriendAsync(int userId, int friendId);

    Task<List<User>> GetFriendsAsync(int userId);
}