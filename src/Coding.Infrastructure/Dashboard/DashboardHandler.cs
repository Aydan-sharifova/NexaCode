using Coding.Application.Abstractions;
using Coding.Application.Features.Dashboard;
using Coding.Data;
using Coding.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Dashboard;

public sealed class GetDashboardHandler(
    AppDbContext db,
    ICurrentUser currentUser,
    ICacheService cache) : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    public Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken ct) =>
        cache.GetOrCreateAsync(
            $"dashboard:user:{currentUser.UserId:N}",
            LoadAsync,
            TimeSpan.FromSeconds(45),
            ct);

    private async Task<DashboardDto> LoadAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow; var today = now.Date; var weekStart = today.AddDays(-6); var previousWeekStart = weekStart.AddDays(-7);
        var projectIds = await db.ProjectMembers.AsNoTracking().Where(x => x.UserId == currentUser.UserId).Select(x => x.ProjectId).ToListAsync(ct);
        var activeProjects = projectIds.Count;
        var priorProjects = await db.ProjectMembers.AsNoTracking().Where(x => x.UserId == currentUser.UserId && x.JoinedAt < weekStart).CountAsync(ct);
        var savesThisWeek = await db.FileVersions.AsNoTracking().CountAsync(x => projectIds.Contains(x.Node.ProjectId) && x.CreatedById == currentUser.UserId && x.CreatAt >= weekStart, ct);
        var savesLastWeek = await db.FileVersions.AsNoTracking().CountAsync(x => projectIds.Contains(x.Node.ProjectId) && x.CreatedById == currentUser.UserId && x.CreatAt >= previousWeekStart && x.CreatAt < weekStart, ct);
        var completed = await db.ProjectTasks.AsNoTracking().CountAsync(x => projectIds.Contains(x.ProjectId) && x.Status == ProjectTaskStatus.Done, ct);
        var totalTasks = await db.ProjectTasks.AsNoTracking().CountAsync(x => projectIds.Contains(x.ProjectId), ct);
        var uniqueMembers = await db.ProjectMembers.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId) && !x.User.IsDeleted).Select(x => x.UserId).Distinct().CountAsync(ct);

        var activityCounts = await db.ActivityLogs.AsNoTracking().Where(x => x.CreatedAt >= weekStart && x.ProjectId.HasValue && projectIds.Contains(x.ProjectId.Value))
            .GroupBy(x => x.CreatedAt.Date).Select(x => new { Date = x.Key, Count = x.Count() }).ToListAsync(ct);
        var weekly = Enumerable.Range(0, 7).Select(offset => weekStart.AddDays(offset)).Select(date => new DashboardPointDto(DateOnly.FromDateTime(date), activityCounts.SingleOrDefault(x => x.Date == date)?.Count ?? 0)).ToList();
        var activity = await db.ActivityLogs.AsNoTracking().Where(x => x.ProjectId.HasValue && projectIds.Contains(x.ProjectId.Value)).OrderByDescending(x => x.CreatedAt).Take(8)
            .Select(x => new DashboardActivityDto(x.Id, x.ActionType, x.Description, x.EntityType, x.EntityId, x.Project == null ? null : x.Project.Name, x.User == null ? null : x.User.FirstName + " " + x.User.LastName, x.CreatedAt)).ToListAsync(ct);
        var projects = await db.Projects.AsNoTracking().Where(x => projectIds.Contains(x.ID)).OrderByDescending(x => x.UpdateAt ?? x.CreatedAt).Take(6)
            .Select(x => new DashboardProjectDto(x.ID, x.Name, x.Description, x.DefaultLanguage,
                x.Tasks.Count == 0 ? 0 : (int)Math.Round(100.0 * x.Tasks.Count(t => t.Status == ProjectTaskStatus.Done) / x.Tasks.Count),
                x.Members.Count(member => !member.User.IsDeleted), x.Tasks.Count(t => t.Status != ProjectTaskStatus.Done), x.UpdateAt ?? x.CreatedAt)).ToListAsync(ct);
        var completion = totalTasks == 0 ? 0 : Math.Round(100m * completed / totalTasks, 1);
        var metrics = new List<DashboardMetricDto>
        {
            new("projects", "Active projects", activeProjects, activeProjects.ToString(), Percent(activeProjects, priorProjects), "from last week"),
            new("saves", "Saves this week", savesThisWeek, savesThisWeek.ToString(), Percent(savesThisWeek, savesLastWeek), "from last week"),
            new("completion", "Task completion", completion, $"{completion:0.#}%", 0, $"{completed} of {totalTasks} tasks"),
            new("members", "Team members", uniqueMembers, uniqueMembers.ToString(), 0, "across your projects")
        };
        return new DashboardDto(metrics, weekly, activity, projects);
    }

    private static decimal Percent(decimal current, decimal previous) => previous == 0 ? current > 0 ? 100 : 0 : Math.Round((current - previous) / previous * 100, 1);
}
