using Microsoft.EntityFrameworkCore;
using Nudge.Api.Models;
namespace Nudge.Api.Data;

public class NudgeDbContext(DbContextOptions<NudgeDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<NudgeMessage> Nudges => Set<NudgeMessage>();
}