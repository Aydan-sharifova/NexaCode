using Coding.Enums;
using FluentValidation;
using MediatR;

namespace Coding.Application.Features.Kanban;

public sealed record TaskAssigneeDto(Guid UserId, string DisplayName, string? AvatarUrl);
public sealed record TaskCommentDto(Guid Id, Guid UserId, string DisplayName, string? AvatarUrl, string Content, DateTime CreatedAt);
public sealed record ProjectTaskDto(Guid Id, Guid ProjectId, string Title, string? Description, ProjectTaskStatus Status, ProjectTaskPriority Priority, decimal Position, DateTime? DueDate, Guid CreatedByUserId, DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<TaskAssigneeDto> Assignees, IReadOnlyList<TaskCommentDto> Comments);

public sealed record CreateTaskCommand(Guid ProjectId, string Title, string? Description, ProjectTaskPriority Priority, DateTime? DueDate) : IRequest<ProjectTaskDto>;
public sealed record UpdateTaskCommand(Guid TaskId, string Title, string? Description, ProjectTaskPriority Priority, DateTime? DueDate) : IRequest<ProjectTaskDto>;
public sealed record DeleteTaskCommand(Guid TaskId) : IRequest;
public sealed record MoveTaskCommand(Guid TaskId, ProjectTaskStatus Status, Guid? PreviousTaskId, Guid? NextTaskId) : IRequest<ProjectTaskDto>;
public sealed record AssignTaskMemberCommand(Guid TaskId, Guid UserId) : IRequest<ProjectTaskDto>;
public sealed record RemoveTaskAssigneeCommand(Guid TaskId, Guid UserId) : IRequest<ProjectTaskDto>;
public sealed record AddTaskCommentCommand(Guid TaskId, string Content) : IRequest<TaskCommentDto>;
public sealed record DeleteTaskCommentCommand(Guid CommentId) : IRequest;
public sealed record GetProjectBoardQuery(Guid ProjectId) : IRequest<IReadOnlyList<ProjectTaskDto>>;

public sealed class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator() { RuleFor(x => x.ProjectId).NotEmpty(); RuleFor(x => x.Title).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).MaximumLength(4000); RuleFor(x => x.DueDate).Must(x => !x.HasValue || x.Value > DateTime.UtcNow).WithMessage("Due date must be in the future."); }
}
public sealed class UpdateTaskValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskValidator() { RuleFor(x => x.TaskId).NotEmpty(); RuleFor(x => x.Title).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).MaximumLength(4000); RuleFor(x => x.DueDate).Must(x => !x.HasValue || x.Value > DateTime.UtcNow).WithMessage("Due date must be in the future."); }
}
public sealed class MoveTaskValidator : AbstractValidator<MoveTaskCommand>
{
    public MoveTaskValidator() { RuleFor(x => x.TaskId).NotEmpty(); RuleFor(x => x).Must(x => x.PreviousTaskId != x.NextTaskId || x.PreviousTaskId is null).WithMessage("Previous and next tasks must be different."); }
}
public sealed class AddTaskCommentValidator : AbstractValidator<AddTaskCommentCommand>
{
    public AddTaskCommentValidator() => RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
}
