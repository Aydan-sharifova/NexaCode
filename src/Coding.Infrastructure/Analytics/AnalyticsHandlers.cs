using Coding.Application.Abstractions;
using Coding.Application.Features.Analytics;
using Coding.Data;
using Coding.Enums;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Analytics;

public sealed class GetAnalyticsDashboardHandler(
    AppDbContext db,
    ICurrentUser currentUser,
    ICacheService cache)
    : IRequestHandler<GetAnalyticsDashboardQuery, AnalyticsDashboardDto>
{
    public Task<AnalyticsDashboardDto> Handle(GetAnalyticsDashboardQuery request, CancellationToken ct)
    {
        var fromKey = request.From?.ToUniversalTime().Ticks ?? 0;
        var toKey = request.To?.ToUniversalTime().Ticks ?? 0;
        return cache.GetOrCreateAsync(
            $"analytics:user:{currentUser.UserId:N}:project:{request.ProjectId?.ToString("N") ?? "all"}:{fromKey}:{toKey}",
            token => LoadAsync(request, token),
            TimeSpan.FromMinutes(2),
            ct);
    }

    private async Task<AnalyticsDashboardDto> LoadAsync(
        GetAnalyticsDashboardQuery request,
        CancellationToken ct)
    {
        var to = (request.To ?? DateTime.UtcNow).ToUniversalTime();
        var from = (request.From ?? to.AddDays(-30)).ToUniversalTime();
        var memberProjects = db.ProjectMembers.AsNoTracking()
            .Where(x => x.UserId == currentUser.UserId)
            .Select(x => x.ProjectId);
        if (request.ProjectId.HasValue)
            memberProjects = memberProjects.Where(x => x == request.ProjectId.Value);

        var activity = db.ActivityLogs.AsNoTracking()
            .Where(x => x.CreatedAt >= from && x.CreatedAt <= to && x.ProjectId.HasValue && memberProjects.Contains(x.ProjectId.Value));
        var tasks = db.ProjectTasks.AsNoTracking().Where(x => memberProjects.Contains(x.ProjectId)&&x.CreatedAt>=from&&x.CreatedAt<=to);
        var completedTasks = await tasks.CountAsync(x => x.Status == ProjectTaskStatus.Done, ct);
        var totalTasks = await tasks.CountAsync(ct);

        var activeUserCounts = await activity.Where(x => x.UserId.HasValue)
            .GroupBy(x => x.UserId!.Value)
            .Select(x => new { UserId = x.Key, ActivityCount = x.Count() })
            .OrderByDescending(x => x.ActivityCount).Take(10).ToListAsync(ct);
        var activeUserIds = activeUserCounts.Select(x => x.UserId).ToList();
        var activeUserProfiles = await db.Users.AsNoTracking().Where(x => activeUserIds.Contains(x.ID))
            .Select(x => new { x.ID, x.FirstName, x.LastName, x.UserName, x.AvatarUrl }).ToListAsync(ct);
        var activeUsers = activeUserCounts.Join(activeUserProfiles, count => count.UserId, user => user.ID,
            (count, user) => new ActiveUserDto(user.ID, user.FirstName + " " + user.LastName, user.UserName, user.AvatarUrl, count.ActivityCount)).ToList();

        var projectCreatedDates = await db.Projects.AsNoTracking()
            .Where(x => memberProjects.Contains(x.ID) && x.CreatedAt >= from && x.CreatedAt <= to)
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);
        var projectsOverTime = projectCreatedDates
            .GroupBy(x => x.Date)
            .Select(x => new TimeSeriesPointDto(x.Key, x.Count()))
            .OrderBy(x => x.Period)
            .ToList();

        var projectLanguages = await db.Projects.AsNoTracking()
            .Where(x => memberProjects.Contains(x.ID))
            .Select(x => x.DefaultLanguage)
            .ToListAsync(ct);
        var languages = projectLanguages
            .GroupBy(x => string.IsNullOrWhiteSpace(x) ? "Other" : x, StringComparer.OrdinalIgnoreCase)
            .Select(x => new LanguageUsageDto(x.Key, x.Count()))
            .OrderByDescending(x => x.ProjectCount).Take(8).ToList();

        var activityDates = await activity.Select(x => x.CreatedAt).ToListAsync(ct);
        var weekly = activityDates
            .GroupBy(x => x.Date)
            .Select(x => new TimeSeriesPointDto(x.Key, x.Count()))
            .OrderBy(x => x.Period).ToList();

        var monthly = activityDates
            .GroupBy(x => new { x.Year, x.Month })
            .Select(x => new TimeSeriesPointDto(new DateTime(x.Key.Year, x.Key.Month, 1, 0, 0, 0, DateTimeKind.Utc), x.Count()))
            .OrderBy(x => x.Period).ToList();

        var fileChanges = await db.FileVersions.AsNoTracking()
            .CountAsync(x => memberProjects.Contains(x.Node.ProjectId) && x.CreatAt >= from && x.CreatAt <= to, ct);
        var projectsCreated = await db.Projects.AsNoTracking()
            .CountAsync(x => memberProjects.Contains(x.ID) && x.CreatedAt >= from && x.CreatedAt <= to, ct);
        var sessionRanges = await db.CodingSessions.AsNoTracking()
            .Where(x => memberProjects.Contains(x.ProjectId) && x.StartAt <= to && (x.EndAt == null || x.EndAt >= from))
            .Select(x => new { x.StartAt, x.EndAt, x.LastActivityAt })
            .ToListAsync(ct);
        var sessionMinutes = sessionRanges.Sum(x => Math.Min(
            ((x.EndAt ?? x.LastActivityAt) - x.StartAt).TotalMinutes,
            30));

        var developer=new DeveloperAnalyticsDto(
            await db.GitCommits.CountAsync(x=>x.UserId==currentUser.UserId&&x.CommitDate>=from&&x.CommitDate<=to&&memberProjects.Contains(x.ProjectId),ct),
            await db.PullRequests.CountAsync(x=>x.AuthorId==currentUser.UserId&&x.CreatedAt>=from&&x.CreatedAt<=to&&memberProjects.Contains(x.ProjectId),ct),
            await db.PullRequestReviews.CountAsync(x=>x.ReviewerId==currentUser.UserId&&x.CreatedAt>=from&&x.CreatedAt<=to&&memberProjects.Contains(x.PullRequest.ProjectId),ct),
            await db.ActivityLogs.CountAsync(x=>x.UserId==currentUser.UserId&&x.ActionType=="DeploymentSucceeded"&&x.CreatedAt>=from&&x.CreatedAt<=to&&(x.ProjectId==null||memberProjects.Contains(x.ProjectId.Value)),ct),
            await db.Projects.CountAsync(x=>memberProjects.Contains(x.ID),ct),
            await activity.CountAsync(x=>x.UserId==currentUser.UserId,ct),
            await db.UserFollows.CountAsync(x=>x.FollowingId==currentUser.UserId&&x.CreatedAt>=from&&x.CreatedAt<=to,ct),
            await db.SocialPosts.CountAsync(x=>x.AuthorId==currentUser.UserId&&x.Type!=PostType.Code&&x.CreatedAt>=from&&x.CreatedAt<=to,ct),
            await db.SocialPosts.CountAsync(x=>x.AuthorId==currentUser.UserId&&x.Type==PostType.Code&&x.CreatedAt>=from&&x.CreatedAt<=to,ct));
        var projectRows=await db.Projects.AsNoTracking().Where(x=>memberProjects.Contains(x.ID)).Select(x=>new{x.ID,x.Name,x.IsPublic,
            Views=db.ProjectViews.Count(v=>v.ProjectId==x.ID&&v.ViewedAt>=from&&v.ViewedAt<=to),
            Likes=db.SocialPostReactions.Count(r=>r.Post.ProjectId==x.ID&&r.CreatedAt>=from&&r.CreatedAt<=to),
            Saves=db.SavedProjects.Count(s=>s.ProjectId==x.ID&&s.CreatedAt>=from&&s.CreatedAt<=to),Contributors=x.Members.Count,
            Deployments=db.ActivityLogs.Count(a=>a.ProjectId==x.ID&&a.ActionType=="DeploymentSucceeded"&&a.CreatedAt>=from&&a.CreatedAt<=to),
            Activity=db.ActivityLogs.Count(a=>a.ProjectId==x.ID&&a.CreatedAt>=from&&a.CreatedAt<=to)}).OrderByDescending(x=>x.Activity).Take(100).ToListAsync(ct);
        var projectAnalytics=projectRows.Select(x=>new ProjectAnalyticsDto(x.ID,x.Name,x.IsPublic,x.Views,0,false,x.Likes,x.Saves,x.Contributors,x.Deployments,x.Activity)).ToList();
        return new AnalyticsDashboardDto(from, to,
            new AnalyticsSummaryDto(
                activeUsers.Count,
                projectsCreated,
                totalTasks == 0 ? 0 : Math.Round(100m * completedTasks / totalTasks, 1),
                fileChanges,
                Math.Round((decimal)sessionMinutes / 60m, 1)),
            activeUsers, projectsOverTime, languages, weekly, monthly,developer,projectAnalytics);
    }
}

public sealed class StartCodingSessionHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<StartCodingSessionCommand, Guid>
{
    public async Task<Guid> Handle(StartCodingSessionCommand request, CancellationToken ct)
    {
        var allowed = await db.ProjectMembers.AsNoTracking()
            .AnyAsync(x => x.ProjectId == request.ProjectId && x.UserId == currentUser.UserId, ct);
        var validFile = await db.WorkspaceNodes.AsNoTracking()
            .AnyAsync(x => x.ID == request.FileId && x.ProjectId == request.ProjectId && x.NodeType == WorkspaceNodeType.File, ct);
        if (!allowed || !validFile) throw new UnauthorizedAccessException("Project membership is required.");

        var now = DateTime.UtcNow;
        var existing = await db.CodingSessions
            .Where(x => x.UserId == currentUser.UserId && x.FileId == request.FileId && x.EndAt == null)
            .OrderByDescending(x => x.StartAt).FirstOrDefaultAsync(ct);
        if (existing is not null && existing.LastActivityAt >= now.AddMinutes(-2))
        {
            existing.LastActivityAt = now;
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }
        if (existing is not null) existing.EndAt = existing.LastActivityAt;

        var session = new CodingSession
        {
            Id = Guid.NewGuid(), UserId = currentUser.UserId, ProjectId = request.ProjectId,
            FileId = request.FileId, StartAt = now, LastActivityAt = now
        };
        db.CodingSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session.Id;
    }
}

public sealed class HeartbeatCodingSessionHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<HeartbeatCodingSessionCommand>
{
    public async Task Handle(HeartbeatCodingSessionCommand request, CancellationToken ct)
    {
        var session = await db.CodingSessions.SingleOrDefaultAsync(x => x.Id == request.SessionId && x.UserId == currentUser.UserId && x.EndAt == null, ct)
            ?? throw new KeyNotFoundException("Coding session was not found.");
        var now = DateTime.UtcNow;
        if (session.LastActivityAt < now.AddMinutes(-5))
            session.EndAt = session.LastActivityAt;
        else
            session.LastActivityAt = now;
        await db.SaveChangesAsync(ct);
    }
}

public sealed class EndCodingSessionHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<EndCodingSessionCommand>
{
    public async Task Handle(EndCodingSessionCommand request, CancellationToken ct)
    {
        var session = await db.CodingSessions.SingleOrDefaultAsync(x => x.Id == request.SessionId && x.UserId == currentUser.UserId && x.EndAt == null, ct);
        if (session is null) return;
        session.EndAt = DateTime.UtcNow < session.LastActivityAt.AddMinutes(5) ? DateTime.UtcNow : session.LastActivityAt;
        await db.SaveChangesAsync(ct);
    }
}
