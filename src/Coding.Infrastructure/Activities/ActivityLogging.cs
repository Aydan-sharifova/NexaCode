using System.Text.Json;
using Coding.Application.Features.Activities;
using Coding.Data;
using Coding.Models;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Activities;

public sealed class ActivityLogger(AppDbContext db, IHttpContextAccessor accessor) : IActivityLogger
{
    private static readonly string[] SensitiveKeys = ["password", "token", "content", "secret", "authorization", "cookie"];
    public async Task LogAsync(ActivityWrite activity, CancellationToken ct = default)
    {
        var projectId = activity.ProjectId;
        if (projectId is null && activity.EntityId.HasValue && activity.EntityType == nameof(WorkspaceNode))
            projectId = await db.WorkspaceNodes.IgnoreQueryFilters().Where(x => x.ID == activity.EntityId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct);
        if (projectId is null && activity.EntityId.HasValue && activity.EntityType == nameof(FileVersion))
            projectId = await db.FileVersions.IgnoreQueryFilters().Where(x => x.ID == activity.EntityId).Select(x => (Guid?)x.Node.ProjectId).SingleOrDefaultAsync(ct);
        var safeMetadata = (activity.Metadata ?? new Dictionary<string, object?>()).Where(pair => !SensitiveKeys.Any(key => pair.Key.Contains(key, StringComparison.OrdinalIgnoreCase))).ToDictionary(pair => pair.Key, pair => pair.Value);
        var request = accessor.HttpContext?.Request;
        db.ActivityLogs.Add(new ActivityLog { Id = Guid.NewGuid(), UserId = activity.UserId, ProjectId = projectId, ActionType = activity.ActionType, EntityType = activity.EntityType, EntityId = activity.EntityId, Description = activity.Description, Metadata = JsonSerializer.SerializeToDocument(safeMetadata), IpAddress = accessor.HttpContext?.Connection.RemoteIpAddress?.ToString(), UserAgent = request?.Headers.UserAgent.ToString(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }
}

public sealed class GetActivityLogsHandler(AppDbContext db) : IRequestHandler<GetActivityLogsQuery, ActivityPage>
{
    public async Task<ActivityPage> Handle(GetActivityLogsQuery r, CancellationToken ct)
    {
        var page = Math.Max(1, r.Page); var size = Math.Clamp(r.PageSize, 1, 100); var query = db.ActivityLogs.AsNoTracking();
        if (r.UserId.HasValue) query = query.Where(x => x.UserId == r.UserId);
        if (r.ProjectId.HasValue) query = query.Where(x => x.ProjectId == r.ProjectId);
        if (!string.IsNullOrWhiteSpace(r.ActionType)) query = query.Where(x => x.ActionType == r.ActionType);
        if (!string.IsNullOrWhiteSpace(r.EntityType)) query = query.Where(x => x.EntityType == r.EntityType);
        // Date-only query-string values are bound by ASP.NET Core as
        // DateTimeKind.Unspecified. Npgsql rejects those values for PostgreSQL
        // `timestamp with time zone` columns, so normalize every boundary to UTC.
        // Treat `to` as an inclusive calendar day when it has no time component.
        if (r.From.HasValue)
        {
            var fromUtc = NormalizeUtc(r.From.Value);
            query = query.Where(x => x.CreatedAt >= fromUtc);
        }

        if (r.To.HasValue)
        {
            var toUtc = NormalizeUtc(r.To.Value);
            if (r.To.Value.TimeOfDay == TimeSpan.Zero)
            {
                var toExclusiveUtc = toUtc.AddDays(1);
                query = query.Where(x => x.CreatedAt < toExclusiveUtc);
            }
            else
            {
                query = query.Where(x => x.CreatedAt <= toUtc);
            }
        }
        var total = await query.CountAsync(ct);
        var entities = await query.Include(x => x.User).Include(x => x.Project).OrderByDescending(x => x.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var items = entities.Select(x => new ActivityLogDto(x.Id, x.UserId, x.User == null ? null : x.User.FirstName + " " + x.User.LastName, x.ProjectId, x.Project?.Name, x.ActionType, x.EntityType, x.EntityId, x.Description, x.Metadata.RootElement.Clone(), x.IpAddress, x.UserAgent, x.CreatedAt)).ToList();
        return new ActivityPage(items, total, page, size);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
