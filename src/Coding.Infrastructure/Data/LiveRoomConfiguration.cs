using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coding.Data;

public sealed class LiveCodingRoomConfiguration : IEntityTypeConfiguration<LiveCodingRoom>
{
    public void Configure(EntityTypeBuilder<LiveCodingRoom> b)
    {
        b.HasQueryFilter(x => !x.IsDeleted);
        b.Property(x => x.Title).HasMaxLength(180);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.ProblemTitle).HasMaxLength(240);
        b.Property(x => x.ProblemStatement).HasMaxLength(20_000);
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ChallengeType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.StateVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.OwnerId, x.Status, x.ScheduledAt });
        b.HasIndex(x => new { x.ProjectId, x.Status });
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class RoomParticipantConfiguration : IEntityTypeConfiguration<RoomParticipant>
{
    public void Configure(EntityTypeBuilder<RoomParticipant> b)
    {
        b.HasQueryFilter(x => !x.IsDeleted && !x.Room.IsDeleted);
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.RoomId, x.UserId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.Status });
        b.HasOne(x => x.Room).WithMany(x => x.Participants).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.InvitedBy).WithMany().HasForeignKey(x => x.InvitedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RoomMessageConfiguration : IEntityTypeConfiguration<RoomMessage>
{
    public void Configure(EntityTypeBuilder<RoomMessage> b)
    {
        b.HasQueryFilter(x => !x.IsDeleted && !x.Room.IsDeleted);
        b.Property(x => x.Content).HasMaxLength(4000);
        b.HasIndex(x => new { x.RoomId, x.SentAt });
        b.HasOne(x => x.Room).WithMany(x => x.Messages).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RoomTaskConfiguration : IEntityTypeConfiguration<RoomTask>
{
    public void Configure(EntityTypeBuilder<RoomTask> b)
    {
        b.HasQueryFilter(x => !x.IsDeleted && !x.Room.IsDeleted);
        b.Property(x => x.Title).HasMaxLength(240);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.RoomId, x.CreatAt });
        b.HasOne(x => x.Room).WithMany(x => x.Tasks).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RoomReactionConfiguration : IEntityTypeConfiguration<RoomReaction>
{
    public void Configure(EntityTypeBuilder<RoomReaction> b)
    {
        b.HasQueryFilter(x => !x.IsDeleted && !x.Room.IsDeleted);
        b.Property(x => x.Emoji).HasMaxLength(16);
        b.HasIndex(x => new { x.RoomId, x.UserId, x.Emoji }).IsUnique();
        b.HasOne(x => x.Room).WithMany(x => x.Reactions).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RoomInterviewerNoteConfiguration : IEntityTypeConfiguration<RoomInterviewerNote>
{
    public void Configure(EntityTypeBuilder<RoomInterviewerNote> b)
    {
        b.HasQueryFilter(x => !x.IsDeleted && !x.Room.IsDeleted);
        b.Property(x => x.Content).HasMaxLength(8000);
        b.HasIndex(x => new { x.RoomId, x.AuthorId, x.CreatAt });
        b.HasOne(x => x.Room).WithMany(x => x.InterviewerNotes).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}
