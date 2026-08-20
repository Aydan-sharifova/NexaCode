using System.Text.RegularExpressions;
using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Kanban;
using Coding.Application.Features.Notifications;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Kanban;

internal static partial class KanbanSupport
{
    private const decimal Spacing = 1024m;
    [GeneratedRegex(@"(?<![\w])@([A-Za-z0-9_.-]{2,50})")] private static partial Regex MentionRegex();
    public static async Task<(ProjectTask Task, ProjectRole Role)> RequireTask(AppDbContext db, Guid taskId, Guid userId, CancellationToken ct)
    {
        var task = await db.ProjectTasks.Include(x => x.Assignees).Include(x => x.Comments).SingleOrDefaultAsync(x => x.ID == taskId, ct) ?? throw new NotFoundException("Task not found.");
        var role = await ProjectAccess.RequireMemberAsync(db, task.ProjectId, userId, ct);
        return (task, role);
    }
    public static void RequireEditor(ProjectTask task, ProjectRole role, Guid userId)
    {
        if (task.CreatedByUserId != userId && role is not (ProjectRole.Owner or ProjectRole.Admin)) throw new ForbiddenException("Only the task creator or a project manager may edit this task.");
    }
    public static void RequireManager(ProjectRole role)
    {
        if (role is not (ProjectRole.Owner or ProjectRole.Admin)) throw new ForbiddenException("Only project Owners and Admins may perform this action.");
    }
    public static void RequireOwner(ProjectRole role)
    {
        if (role != ProjectRole.Owner) throw new ForbiddenException("Only the project owner may perform this action.");
    }
    public static void RequireBeforeDeadline(ProjectTask task)
    {
        if (task.DueDate.HasValue && task.DueDate.Value <= DateTime.UtcNow) throw new ConflictException("The task deadline has passed. This task is locked.");
    }
    public static IQueryable<ProjectTaskDto> Project(IQueryable<ProjectTask> query) => query.Select(task => new ProjectTaskDto(task.ID, task.ProjectId, task.Title, task.Description, task.Status, task.Priority, task.Position, task.DueDate, task.CreatedByUserId, task.CreatedAt, task.UpdatedAt, task.Assignees.Select(a => new TaskAssigneeDto(a.UserId, a.User.FirstName + " " + a.User.LastName, a.User.AvatarUrl)).ToList(), task.Comments.OrderBy(c => c.CreatedAt).Select(c => new TaskCommentDto(c.ID, c.UserId, c.User.FirstName + " " + c.User.LastName, c.User.AvatarUrl, c.Content, c.CreatedAt)).ToList()));
    public static Task<ProjectTaskDto> Dto(AppDbContext db, Guid taskId, CancellationToken ct) => Project(db.ProjectTasks.AsNoTracking().Where(x => x.ID == taskId)).SingleAsync(ct);
    public static string[] Mentions(string content) => MentionRegex().Matches(content).Select(x => x.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static async Task<decimal> Position(AppDbContext db, Guid projectId, ProjectTaskStatus status, Guid? previousId, Guid? nextId, CancellationToken ct)
    {
        var previous = previousId.HasValue ? await db.ProjectTasks.Where(x => x.ID == previousId && x.ProjectId == projectId && x.Status == status).Select(x => (decimal?)x.Position).SingleOrDefaultAsync(ct) : null;
        var next = nextId.HasValue ? await db.ProjectTasks.Where(x => x.ID == nextId && x.ProjectId == projectId && x.Status == status).Select(x => (decimal?)x.Position).SingleOrDefaultAsync(ct) : null;
        if (previousId.HasValue && previous is null || nextId.HasValue && next is null) throw new ConflictException("The requested neighboring task is not in the target column.");
        if (previous.HasValue && next.HasValue) return (previous.Value + next.Value) / 2m;
        if (previous.HasValue) return previous.Value + Spacing;
        if (next.HasValue) return next.Value - Spacing;
        return await db.ProjectTasks.Where(x => x.ProjectId == projectId && x.Status == status).MaxAsync(x => (decimal?)x.Position, ct) + Spacing ?? Spacing;
    }
}

public sealed class CreateTaskHandler(AppDbContext db, ICurrentUser user, IActivityLogger activity) : IRequestHandler<CreateTaskCommand, ProjectTaskDto>
{
    public async Task<ProjectTaskDto> Handle(CreateTaskCommand r, CancellationToken ct)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, r.ProjectId, user.UserId, ct);
        KanbanSupport.RequireOwner(role);
        var now = DateTime.UtcNow; var position = await KanbanSupport.Position(db, r.ProjectId, ProjectTaskStatus.Todo, null, null, ct);
        var task = new ProjectTask { ID = Guid.NewGuid(), ProjectId = r.ProjectId, Title = r.Title.Trim(), Description = r.Description?.Trim(), Status = ProjectTaskStatus.Todo, Priority = r.Priority, Position = position, DueDate = r.DueDate, CreatedByUserId = user.UserId, CreatedAt = now, UpdatedAt = now, CreatAt = now };
        db.ProjectTasks.Add(task); await db.SaveChangesAsync(ct);
        await activity.LogAsync(new(user.UserId, r.ProjectId, "TaskCreated", nameof(ProjectTask), task.ID, $"Created task '{task.Title}'.", new Dictionary<string, object?> { ["priority"] = task.Priority.ToString(), ["status"] = task.Status.ToString() }), ct);
        return await KanbanSupport.Dto(db, task.ID, ct);
    }
}
public sealed class UpdateTaskHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<UpdateTaskCommand, ProjectTaskDto>
{
    public async Task<ProjectTaskDto> Handle(UpdateTaskCommand r, CancellationToken ct) { var (task, role) = await KanbanSupport.RequireTask(db, r.TaskId, user.UserId, ct); KanbanSupport.RequireEditor(task, role, user.UserId); KanbanSupport.RequireBeforeDeadline(task); task.Title = r.Title.Trim(); task.Description = r.Description?.Trim(); task.Priority = r.Priority; task.DueDate = r.DueDate; var now = DateTime.UtcNow; task.UpdatedAt = now; task.UpdateAt = now; await db.SaveChangesAsync(ct); return await KanbanSupport.Dto(db, task.ID, ct); }
}
public sealed class DeleteTaskHandler(AppDbContext db, ICurrentUser user, IActivityLogger activity) : IRequestHandler<DeleteTaskCommand>
{
    public async Task Handle(DeleteTaskCommand r, CancellationToken ct) { var (task, role) = await KanbanSupport.RequireTask(db, r.TaskId, user.UserId, ct); KanbanSupport.RequireManager(role); task.IsDeleted = true; task.DeletedAt = task.UpdateAt = task.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); await activity.LogAsync(new(user.UserId, task.ProjectId, "TaskDeleted", nameof(ProjectTask), task.ID, $"Deleted task '{task.Title}'."), ct); }
}
public sealed class MoveTaskHandler(AppDbContext db, ICurrentUser user, IActivityLogger activity) : IRequestHandler<MoveTaskCommand, ProjectTaskDto>
{
    public async Task<ProjectTaskDto> Handle(MoveTaskCommand r, CancellationToken ct)
    {
        var (task, role) = await KanbanSupport.RequireTask(db, r.TaskId, user.UserId, ct); KanbanSupport.RequireOwner(role); KanbanSupport.RequireBeforeDeadline(task); var oldStatus = task.Status;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () => { await using var tx = await db.Database.BeginTransactionAsync(ct); task.Status = r.Status; task.Position = await KanbanSupport.Position(db, task.ProjectId, r.Status, r.PreviousTaskId, r.NextTaskId, ct); var now = DateTime.UtcNow; task.UpdatedAt = now; task.UpdateAt = now; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); });
        if (oldStatus != task.Status) await activity.LogAsync(new(user.UserId, task.ProjectId, "TaskStatusChanged", nameof(ProjectTask), task.ID, $"Moved task '{task.Title}' from {oldStatus} to {task.Status}.", new Dictionary<string, object?> { ["from"] = oldStatus.ToString(), ["to"] = task.Status.ToString() }), ct);
        return await KanbanSupport.Dto(db, task.ID, ct);
    }
}
public sealed class AssignTaskMemberHandler(AppDbContext db, ICurrentUser user, INotificationService notifications, IActivityLogger activity) : IRequestHandler<AssignTaskMemberCommand, ProjectTaskDto>
{
    public async Task<ProjectTaskDto> Handle(AssignTaskMemberCommand r, CancellationToken ct) { var (task, role) = await KanbanSupport.RequireTask(db, r.TaskId, user.UserId, ct); KanbanSupport.RequireEditor(task, role, user.UserId); if (!await db.ProjectMembers.AnyAsync(x => x.ProjectId == task.ProjectId && x.UserId == r.UserId, ct)) throw new ForbiddenException("The assignee is not a project member."); if (!await db.TaskAssignees.AnyAsync(x => x.TaskId == task.ID && x.UserId == r.UserId, ct)) { db.TaskAssignees.Add(new TaskAssignee { TaskId = task.ID, UserId = r.UserId, AssignedByUserId = user.UserId, AssignedAt = DateTime.UtcNow }); await db.SaveChangesAsync(ct); if (r.UserId != user.UserId) await notifications.CreateAsync(new(r.UserId, NotificationType.TaskAssignment, "Task assigned", $"You were assigned to '{task.Title}'.", task.ID, nameof(ProjectTask)), ct); await activity.LogAsync(new(user.UserId, task.ProjectId, "TaskAssigned", nameof(ProjectTask), task.ID, $"Assigned a project member to '{task.Title}'.", new Dictionary<string, object?> { ["assigneeUserId"] = r.UserId }), ct); } return await KanbanSupport.Dto(db, task.ID, ct); }
}
public sealed class RemoveTaskAssigneeHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<RemoveTaskAssigneeCommand, ProjectTaskDto>
{
    public async Task<ProjectTaskDto> Handle(RemoveTaskAssigneeCommand r, CancellationToken ct) { var (task, role) = await KanbanSupport.RequireTask(db, r.TaskId, user.UserId, ct); KanbanSupport.RequireEditor(task, role, user.UserId); var item = await db.TaskAssignees.SingleOrDefaultAsync(x => x.TaskId == task.ID && x.UserId == r.UserId, ct) ?? throw new NotFoundException("Task assignee not found."); db.TaskAssignees.Remove(item); await db.SaveChangesAsync(ct); return await KanbanSupport.Dto(db, task.ID, ct); }
}
public sealed class AddTaskCommentHandler(AppDbContext db, ICurrentUser user, INotificationService notifications) : IRequestHandler<AddTaskCommentCommand, TaskCommentDto>
{
    public async Task<TaskCommentDto> Handle(AddTaskCommentCommand r, CancellationToken ct)
    {
        var (task, _) = await KanbanSupport.RequireTask(db, r.TaskId, user.UserId, ct); var names = KanbanSupport.Mentions(r.Content); var mentioned = names.Length == 0 ? [] : await db.Users.Where(x => names.Contains(x.UserName) && x.ID != user.UserId).Select(x => new { x.ID, x.UserName }).ToListAsync(ct); if (mentioned.Count != names.Length) throw new FluentValidation.ValidationException("One or more mentioned users do not exist."); var allowed = await db.ProjectMembers.CountAsync(x => x.ProjectId == task.ProjectId && mentioned.Select(m => m.ID).Contains(x.UserId), ct); if (allowed != mentioned.Count) throw new ForbiddenException("A mentioned user is not a project member.");
        var comment = new TaskComment { ID = Guid.NewGuid(), TaskId = task.ID, UserId = user.UserId, Content = r.Content.Trim(), CreatedAt = DateTime.UtcNow }; db.TaskComments.Add(comment); await db.SaveChangesAsync(ct);
        if (mentioned.Count > 0) await notifications.CreateManyAsync(mentioned.Select(x => new CreateNotificationRequest(x.ID, NotificationType.UserMention, "Mentioned in a task comment", r.Content, task.ID, nameof(ProjectTask))), ct);
        return await db.TaskComments.AsNoTracking().Where(x => x.ID == comment.ID).Select(x => new TaskCommentDto(x.ID, x.UserId, x.User.FirstName + " " + x.User.LastName, x.User.AvatarUrl, x.Content, x.CreatedAt)).SingleAsync(ct);
    }
}
public sealed class DeleteTaskCommentHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<DeleteTaskCommentCommand>
{
    public async Task Handle(DeleteTaskCommentCommand r, CancellationToken ct) { var comment = await db.TaskComments.Include(x => x.Task).SingleOrDefaultAsync(x => x.ID == r.CommentId, ct) ?? throw new NotFoundException("Comment not found."); var role = await ProjectAccess.RequireMemberAsync(db, comment.Task.ProjectId, user.UserId, ct); if (comment.UserId != user.UserId && role is not (ProjectRole.Owner or ProjectRole.Admin)) throw new ForbiddenException("You can delete only your own comments."); comment.IsDeleted = true; comment.DeletedAt = comment.UpdateAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
}
public sealed class GetProjectBoardHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetProjectBoardQuery, IReadOnlyList<ProjectTaskDto>>
{
    public async Task<IReadOnlyList<ProjectTaskDto>> Handle(GetProjectBoardQuery r, CancellationToken ct) { await ProjectAccess.RequireMemberAsync(db, r.ProjectId, user.UserId, ct); return await KanbanSupport.Project(db.ProjectTasks.AsNoTracking().Where(x => x.ProjectId == r.ProjectId).OrderBy(x => x.Status).ThenBy(x => x.Position)).ToListAsync(ct); }
}
