using Coding.Enums;
using FluentValidation;
using MediatR;

namespace Coding.Application.Features.Projects;

public sealed record ProjectListItem(Guid Id, string Name, string? Description, string DefaultLanguage, ProjectRole CurrentUserRole, int MemberCount, DateTime CreatedAt, DateTime? DeadlineAt, ProjectStatus Status, bool IsReadOnly);
public sealed record ProjectDetails(Guid Id, string Name, string? Description, string DefaultLanguage, bool IsPublic, Guid OwnerId, ProjectRole CurrentUserRole, DateTime CreatedAt, DateTime? UpdatedAt, DateTime? DeadlineAt, ProjectStatus Status, bool IsReadOnly);
public sealed record ProjectMemberDetails(Guid UserId, string PublicId, string FullName, string Email, string? AvatarUrl, ProjectRole Role, DateTime JoinedAt);
public sealed record ProjectInvitationDetails(Guid Id, string Email, ProjectRole Role, DateTime ExpiresAt, string InvitedBy);
public sealed record CreatedInvitation(Guid Id, string Token, DateTime ExpiresAt);
public sealed record ProjectDeadlineState(Guid ProjectId, DateTime DeadlineAt, ProjectStatus Status);

public sealed record CreateProjectCommand(string Name, string? Description, string DefaultLanguage, bool IsPublic, DateTime? DeadlineAt = null) : IRequest<ProjectDetails>;
public sealed record UpdateProjectCommand(Guid ProjectId, string Name, string? Description, string DefaultLanguage, bool IsPublic) : IRequest<ProjectDetails>;
public sealed record DeleteProjectCommand(Guid ProjectId) : IRequest;
public sealed record InviteProjectMemberCommand(Guid ProjectId, string Email, ProjectRole Role) : IRequest<CreatedInvitation>;
public sealed record AcceptProjectInvitationCommand(string Token) : IRequest<Guid>;
public sealed record RejectProjectInvitationCommand(string Token) : IRequest;
public sealed record AcceptProjectInvitationByIdCommand(Guid InvitationId) : IRequest<Guid>;
public sealed record RejectProjectInvitationByIdCommand(Guid InvitationId) : IRequest;
public sealed record ChangeProjectMemberRoleCommand(Guid ProjectId, Guid UserId, ProjectRole Role) : IRequest;
public sealed record RemoveProjectMemberCommand(Guid ProjectId, Guid UserId) : IRequest;
public sealed record ListMyProjectsQuery : IRequest<IReadOnlyList<ProjectListItem>>;
public sealed record GetProjectDetailsQuery(Guid ProjectId) : IRequest<ProjectDetails>;
public sealed record ListProjectMembersQuery(Guid ProjectId) : IRequest<IReadOnlyList<ProjectMemberDetails>>;
public sealed record ListPendingInvitationsQuery(Guid ProjectId) : IRequest<IReadOnlyList<ProjectInvitationDetails>>;
public sealed record ExtendProjectDeadlineCommand(Guid ProjectId, DateTime DeadlineAt) : IRequest<ProjectDeadlineState>;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.DefaultLanguage).NotEmpty().MaximumLength(50);
        RuleFor(command => command.DeadlineAt).Must(value => !value.HasValue || value.Value > DateTime.UtcNow)
            .WithMessage("Project deadline must be in the future.");
    }
}

public sealed class ExtendProjectDeadlineValidator : AbstractValidator<ExtendProjectDeadlineCommand>
{
    public ExtendProjectDeadlineValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.DeadlineAt).GreaterThan(DateTime.UtcNow);
    }
}

public sealed class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.DefaultLanguage).NotEmpty().MaximumLength(50);
    }
}

public sealed class InviteProjectMemberValidator : AbstractValidator<InviteProjectMemberCommand>
{
    public InviteProjectMemberValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(command => command.Role).Must(role => role is ProjectRole.Admin or ProjectRole.Maintainer or ProjectRole.Developer or ProjectRole.Viewer)
            .WithMessage("Invitations may grant Admin, Maintainer, Developer, or Viewer roles.");
    }
}

public sealed class ChangeProjectMemberRoleValidator : AbstractValidator<ChangeProjectMemberRoleCommand>
{
    public ChangeProjectMemberRoleValidator() => RuleFor(command => command.Role)
        .Must(role => role is ProjectRole.Admin or ProjectRole.Maintainer or ProjectRole.Developer or ProjectRole.Viewer)
        .WithMessage("Use ownership transfer to change the Owner role.");
}
