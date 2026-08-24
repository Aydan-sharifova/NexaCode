using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Moderation;
using Coding.Data;
using Coding.Domain.Services;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Authentication;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Moderation;

internal static class ModerationSupport
{
    public static ModerationUser User(User x) => new(x.ID, x.PublicId, x.UserName, $"{x.FirstName} {x.LastName}".Trim());
    public static async Task RequireModeratorAsync(AppDbContext db, Guid userId, CancellationToken ct)
    {
        if (!await db.UserRoles.AnyAsync(x => x.UserId == userId && (x.Role.Name == SystemRoles.Moderator || x.Role.Name == SystemRoles.Admin || x.Role.Name == SystemRoles.SuperAdmin), ct))
            throw new ForbiddenException("Moderator access is required.");
    }

    public static async Task<(string Label, Guid OwnerId)> RequireTargetAsync(AppDbContext db, ReportTargetType type, Guid id, CancellationToken ct)
    {
        return type switch
        {
            ReportTargetType.Post => await db.SocialPosts.Where(x => x.ID == id).Select(x => new ValueTuple<string, Guid>(x.Content.Length > 80 ? x.Content.Substring(0, 80) : x.Content, x.AuthorId)).SingleOrDefaultAsync(ct) is var post && post.Item2 != Guid.Empty ? post : throw new NotFoundException("Post not found."),
            ReportTargetType.Snippet => await db.SocialPosts.Where(x => x.ID == id && x.Type == PostType.Code).Select(x => new ValueTuple<string, Guid>(x.Content.Length > 80 ? x.Content.Substring(0, 80) : x.Content, x.AuthorId)).SingleOrDefaultAsync(ct) is var snippet && snippet.Item2 != Guid.Empty ? snippet : throw new NotFoundException("Snippet not found."),
            ReportTargetType.Comment => await db.SocialPostComments.Where(x => x.ID == id).Select(x => new ValueTuple<string, Guid>(x.Content.Length > 80 ? x.Content.Substring(0, 80) : x.Content, x.AuthorId)).SingleOrDefaultAsync(ct) is var comment && comment.Item2 != Guid.Empty ? comment : throw new NotFoundException("Comment not found."),
            ReportTargetType.Project => await db.Projects.Where(x => x.ID == id && x.IsPublic).Select(x => new ValueTuple<string, Guid>(x.Name, x.OwnerId)).SingleOrDefaultAsync(ct) is var project && project.Item2 != Guid.Empty ? project : throw new NotFoundException("Public project not found."),
            ReportTargetType.Profile => await db.Users.Where(x => x.ID == id && !x.IsDeleted).Select(x => new ValueTuple<string, Guid>("@" + x.UserName, x.ID)).SingleOrDefaultAsync(ct) is var profile && profile.Item2 != Guid.Empty ? profile : throw new NotFoundException("Profile not found."),
            _ => throw new NotFoundException("Report target not found.")
        };
    }

    public static async Task RequireViewerAccessAsync(AppDbContext db, ReportTargetType type, Guid id, Guid viewerId, Guid ownerId, CancellationToken ct)
    {
        if (await db.UserBlocks.AnyAsync(x => (x.BlockerId == viewerId && x.BlockedId == ownerId) || (x.BlockerId == ownerId && x.BlockedId == viewerId), ct)) throw new NotFoundException("Report target not found.");
        var visible = type switch
        {
            ReportTargetType.Post or ReportTargetType.Snippet => await db.SocialPosts.AnyAsync(x => x.ID == id && (x.ProjectId == null || x.Project!.IsPublic || x.Project.Members.Any(m => m.UserId == viewerId)), ct),
            ReportTargetType.Comment => await db.SocialPostComments.AnyAsync(x => x.ID == id && (x.Post.ProjectId == null || x.Post.Project!.IsPublic || x.Post.Project.Members.Any(m => m.UserId == viewerId)), ct),
            ReportTargetType.Profile => await db.Users.AnyAsync(x => x.ID == id && (x.DeveloperProfile == null || x.DeveloperProfile.IsProfilePublic), ct),
            _ => true
        };
        if (!visible) throw new NotFoundException("Report target not found.");
    }

    public static async Task<string> TargetLabelAsync(AppDbContext db, ReportTargetType type, Guid id, CancellationToken ct)
    {
        try { return (await RequireTargetAsync(db, type, id, ct)).Label; } catch (NotFoundException) { return "Removed content"; }
    }

    public static async Task<ContentReportItem> ItemAsync(AppDbContext db, ContentReport report, CancellationToken ct)
    {
        await db.Entry(report).Reference(x => x.Reporter).LoadAsync(ct);
        if (report.AssignedModeratorId.HasValue) await db.Entry(report).Reference(x => x.AssignedModerator).LoadAsync(ct);
        await db.Entry(report).Collection(x => x.Actions).Query().Include(x => x.Moderator).OrderBy(x => x.CreatAt).LoadAsync(ct);
        return new(report.ID, User(report.Reporter), report.TargetType, report.TargetId, await TargetLabelAsync(db, report.TargetType, report.TargetId, ct), report.Reason, report.Details, report.State, report.AssignedModerator is null ? null : User(report.AssignedModerator), report.CreatAt, report.ReviewedAt, report.Actions.OrderBy(x => x.CreatAt).Select(x => new ModerationActionItem(x.ID, User(x.Moderator), x.Action, x.PreviousState, x.NewState, x.Note, x.CreatAt)).ToArray());
    }
}

public sealed class CreateContentReportHandler(AppDbContext db, ICurrentUser user, IActivityLogger audit) : IRequestHandler<CreateContentReportCommand, ContentReportItem>
{
    public async Task<ContentReportItem> Handle(CreateContentReportCommand r, CancellationToken ct)
    {
        var target = await ModerationSupport.RequireTargetAsync(db, r.TargetType, r.TargetId, ct);
        if (r.TargetType == ReportTargetType.Post && await db.SocialPosts.AnyAsync(x => x.ID == r.TargetId && x.Type == PostType.Code, ct)) throw new ConflictException("Code posts must be reported as snippets.");
        await ModerationSupport.RequireViewerAccessAsync(db, r.TargetType, r.TargetId, user.UserId, target.OwnerId, ct);
        if (target.OwnerId == user.UserId) throw new ConflictException("You cannot report your own content or profile.");
        if (await db.ContentReports.AnyAsync(x => x.ReporterId == user.UserId && x.TargetType == r.TargetType && x.TargetId == r.TargetId && (x.State == ModerationReportState.Pending || x.State == ModerationReportState.Reviewing), ct))
            throw new ConflictException("You already have an open report for this content.");
        var report = new ContentReport { ID = Guid.NewGuid(), ReporterId = user.UserId, TargetType = r.TargetType, TargetId = r.TargetId, Reason = r.Reason, Details = r.Details?.Trim() };
        db.ContentReports.Add(report); await db.SaveChangesAsync(ct);
        await audit.LogAsync(new(user.UserId, r.TargetType == ReportTargetType.Project ? r.TargetId : null, "ContentReported", r.TargetType.ToString(), r.TargetId, "A content report was submitted.", new Dictionary<string, object?> { ["reportId"] = report.ID, ["reason"] = r.Reason }), ct);
        return await ModerationSupport.ItemAsync(db, report, ct);
    }
}

public sealed class GetMyContentReportsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetMyContentReportsQuery, ModerationQueue>
{
    public async Task<ModerationQueue> Handle(GetMyContentReportsQuery r, CancellationToken ct)
    {
        var page = Math.Max(1, r.Page); var size = Math.Clamp(r.PageSize, 1, 100); var query = db.ContentReports.Where(x => x.ReporterId == user.UserId);
        var total = await query.CountAsync(ct); var reports = await query.OrderByDescending(x => x.CreatAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var items = new List<ContentReportItem>(); foreach (var report in reports) items.Add(await ModerationSupport.ItemAsync(db, report, ct)); return new(items, total, page, size);
    }
}

public sealed class GetModerationQueueHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetModerationQueueQuery, ModerationQueue>
{
    public async Task<ModerationQueue> Handle(GetModerationQueueQuery r, CancellationToken ct)
    {
        await ModerationSupport.RequireModeratorAsync(db, user.UserId, ct); var page = Math.Max(1, r.Page); var size = Math.Clamp(r.PageSize, 1, 100); var query = db.ContentReports.AsQueryable();
        if (r.State.HasValue) query = query.Where(x => x.State == r.State); if (r.TargetType.HasValue) query = query.Where(x => x.TargetType == r.TargetType);
        var total = await query.CountAsync(ct); var reports = await query.OrderBy(x => x.State).ThenBy(x => x.CreatAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var items = new List<ContentReportItem>(); foreach (var report in reports) items.Add(await ModerationSupport.ItemAsync(db, report, ct)); return new(items, total, page, size);
    }
}

public sealed class ModerateContentReportHandler(AppDbContext db, ICurrentUser user, IActivityLogger audit) : IRequestHandler<ModerateContentReportCommand, ContentReportItem>
{
    public async Task<ContentReportItem> Handle(ModerateContentReportCommand r, CancellationToken ct)
    {
        await ModerationSupport.RequireModeratorAsync(db, user.UserId, ct);
        var report = await db.ContentReports.SingleOrDefaultAsync(x => x.ID == r.ReportId, ct) ?? throw new NotFoundException("Content report not found.");
        var next = ModerationLifecycle.Next(report.State, r.Action) ?? throw new ConflictException($"{r.Action} is not valid while the report is {report.State}.");
        if (r.Action == ModerationActionType.SuspendProfile && report.TargetType != ReportTargetType.Profile) throw new ConflictException("SuspendProfile applies only to profile reports.");
        if (r.Action == ModerationActionType.RemoveContent && report.TargetType == ReportTargetType.Profile) throw new ConflictException("Use SuspendProfile for a profile report.");
        if (r.Action == ModerationActionType.RemoveContent) await RemoveTargetAsync(db, report, ct);
        if (r.Action == ModerationActionType.SuspendProfile) { var profile = await db.Users.SingleOrDefaultAsync(x => x.ID == report.TargetId && !x.IsDeleted, ct) ?? throw new NotFoundException("Profile not found."); profile.IsSuspended = true; profile.SuspensionReason = $"Moderation report {report.ID}: {r.Note.Trim()}"; }
        var previous = report.State; report.State = next; report.AssignedModeratorId = next == ModerationReportState.Pending ? null : user.UserId; report.ReviewedAt = next is ModerationReportState.ActionTaken or ModerationReportState.Dismissed ? DateTime.UtcNow : null; report.UpdateAt = DateTime.UtcNow;
        db.ModerationActionRecords.Add(new() { ID = Guid.NewGuid(), ReportId = report.ID, ModeratorId = user.UserId, Action = r.Action, PreviousState = previous, NewState = next, Note = r.Note.Trim() });
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(new(user.UserId, report.TargetType == ReportTargetType.Project ? report.TargetId : null, "ModerationAction", nameof(ContentReport), report.ID, $"Moderator applied {r.Action}.", new Dictionary<string, object?> { ["targetType"] = report.TargetType, ["targetId"] = report.TargetId, ["previousState"] = previous, ["newState"] = next }), ct);
        return await ModerationSupport.ItemAsync(db, report, ct);
    }

    private static async Task RemoveTargetAsync(AppDbContext db, ContentReport report, CancellationToken ct)
    {
        Base target = report.TargetType switch
        {
            ReportTargetType.Post or ReportTargetType.Snippet => await db.SocialPosts.SingleOrDefaultAsync(x => x.ID == report.TargetId, ct) ?? throw new NotFoundException("Post not found."),
            ReportTargetType.Comment => await db.SocialPostComments.SingleOrDefaultAsync(x => x.ID == report.TargetId, ct) ?? throw new NotFoundException("Comment not found."),
            ReportTargetType.Project => await db.Projects.SingleOrDefaultAsync(x => x.ID == report.TargetId, ct) ?? throw new NotFoundException("Project not found."),
            _ => throw new ConflictException("This target cannot be removed as content.")
        };
        target.IsDeleted = true; target.DeletedAt = DateTime.UtcNow;
    }
}
