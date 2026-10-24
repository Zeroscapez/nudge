using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;

namespace Nudge.Api.Infrastructure;

// Tells SignalR which user a connection belongs to, so the server can call
// Clients.User("5") to reach every device user 5 has open.
public class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
