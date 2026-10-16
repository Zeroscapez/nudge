using Nudge.Api.Models;
using Nudge.Api.Data;      // only needed in the classes that use db

namespace Nudge.Api.Services;

public interface IUserService
{
    Task<User> CreateAsync(string name);
    Task<User> GetAsync(int id);

    Task<User> AddFriendAsync(int userId, int friendId);

    Task<List<User>> GetFriendsAsync(int userId);
}