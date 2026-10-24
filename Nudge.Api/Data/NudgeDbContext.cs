using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nudge.Api.Models;

namespace Nudge.Api.Data;

public class NudgeDbContext(DbContextOptions<NudgeDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();
    public DbSet<NudgeMessage> Nudges => Set<NudgeMessage>();

    // SQLite has no real date type, so dates come back with Kind = Unspecified and would be sent
    // to the browser without a "Z", making it think they're local time. Mark every date as UTC.
    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        c.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        c.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? v.Value.ToUniversalTime() : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(20);
            e.Property(u => u.DisplayName).HasMaxLength(40);
            e.Property(u => u.MoodEmoji).HasMaxLength(16);
            e.Property(u => u.MoodText).HasMaxLength(30);
        });

        b.Entity<Friendship>(e =>
        {
            e.HasIndex(f => new { f.UserId, f.FriendId }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Friend).WithMany().HasForeignKey(f => f.FriendId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<FriendRequest>(e =>
        {
            e.HasIndex(r => new { r.FromUserId, r.ToUserId }).IsUnique();
            e.HasOne(r => r.FromUser).WithMany().HasForeignKey(r => r.FromUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.ToUser).WithMany().HasForeignKey(r => r.ToUserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Room>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(40);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RoomMember>(e =>
        {
            e.HasKey(m => new { m.RoomId, m.UserId });   // composite key: a user is in a room at most once
            e.HasOne(m => m.Room).WithMany(r => r.Members).HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<NudgeMessage>(e =>
        {
            e.Property(n => n.Note).HasMaxLength(NudgeMessage.MaxNoteLength);
            e.Property(n => n.Emojis).HasMaxLength(32);
            e.HasIndex(n => new { n.ToUserId, n.SentAt });
            e.HasIndex(n => new { n.RoomId, n.SentAt });
            e.HasOne(n => n.FromUser).WithMany().HasForeignKey(n => n.FromUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(n => n.ToUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Room>().WithMany().HasForeignKey(n => n.RoomId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_Nudge_OneTarget",
                "(\"ToUserId\" IS NULL) <> (\"RoomId\" IS NULL)"));
        });
    }
}
