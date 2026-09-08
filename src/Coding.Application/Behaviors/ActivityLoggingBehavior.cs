using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.FileExplorer;
using Coding.Application.Features.Projects;
using MediatR;

namespace Coding.Application.Behaviors;

public sealed class ActivityLoggingBehavior<TRequest, TResponse>(IActivityLogger logger, ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next();
        var activity = Map(request, response);
        if (activity is not null) await logger.LogAsync(activity with { UserId = currentUser.UserId }, ct);
        return response;
    }

    private static ActivityWrite? Map(TRequest request, TResponse response) => request switch
    {
        CreateProjectCommand => new(null, GetGuid(response, "Id"), "ProjectCreated", "Project", GetGuid(response, "Id"), "Created a project."),
        UpdateProjectCommand x => new(null, x.ProjectId, "ProjectUpdated", "Project", x.ProjectId, "Updated project settings."),
        DeleteProjectCommand x => new(null, x.ProjectId, "ProjectDeleted", "Project", x.ProjectId, "Deleted a project."),
        ChangeProjectLifecycleCommand x => new(null, x.ProjectId, $"Project{ x.Status }", "Project", x.ProjectId, $"Changed project lifecycle to {x.Status}."),
        InviteProjectMemberCommand x => new(null, x.ProjectId, "ProjectInvitationCreated", "ProjectInvitation", GetGuid(response, "Id"), "Invited a project member."),
        AcceptProjectInvitationCommand => new(null, response is Guid projectId ? projectId : null, "ProjectInvitationAccepted", "ProjectInvitation", null, "Accepted a project invitation."),
        ChangeProjectMemberRoleCommand x => new(null, x.ProjectId, "ProjectMemberRoleChanged", "ProjectMember", x.UserId, "Changed a project member role.", new Dictionary<string, object?> { ["role"] = x.Role.ToString() }),
        RemoveProjectMemberCommand x => new(null, x.ProjectId, "ProjectMemberRemoved", "ProjectMember", x.UserId, "Removed a project member."),
        CreateFileCommand x => new(null, x.ProjectId, "FileCreated", "WorkspaceNode", GetGuid(response, "Id"), $"Created file '{x.Name}'."),
        CreateFolderCommand x => new(null, x.ProjectId, "FolderCreated", "WorkspaceNode", GetGuid(response, "Id"), $"Created folder '{x.Name}'."),
        RenameNodeCommand x => new(null, GetGuid(response, "ProjectId"), "NodeRenamed", "WorkspaceNode", x.NodeId, $"Renamed a workspace node to '{x.Name}'."),
        MoveNodeCommand x => new(null, GetGuid(response, "ProjectId"), "NodeMoved", "WorkspaceNode", x.NodeId, "Moved a workspace node."),
        DeleteNodeCommand x => new(null, null, "NodeDeleted", "WorkspaceNode", x.NodeId, "Deleted a workspace node."),
        RestoreDeletedNodeCommand x => new(null, null, "NodeRestored", "WorkspaceNode", x.NodeId, "Restored a workspace node."),
        RestoreFileVersionCommand x => new(null, null, "FileVersionRestored", "FileVersion", x.VersionId, "Restored a file version.", new Dictionary<string, object?> { ["nodeId"] = x.NodeId }),
        _ => null
    };

    private static Guid? GetGuid(object? value, string property) =>
        value?.GetType().GetProperty(property)?.GetValue(value) is Guid id ? id : null;
}
