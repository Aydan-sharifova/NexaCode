using Coding.Application.Features.Projects;
using Coding.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public sealed class ProjectController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ProjectListItem>> List(CancellationToken cancellationToken) => await sender.Send(new ListMyProjectsQuery(), cancellationToken);

    [HttpGet("{projectId:guid}")]
    public async Task<ProjectDetails> Get(Guid projectId, CancellationToken cancellationToken) => await sender.Send(new GetProjectDetailsQuery(projectId), cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ProjectDetails>> Create(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await sender.Send(new CreateProjectCommand(request.Name, request.Description, request.DefaultLanguage, request.IsPublic), cancellationToken);
        return CreatedAtAction(nameof(Get), new { projectId = project.Id }, project);
    }

    [HttpPut("{projectId:guid}")]
    public Task<ProjectDetails> Update(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken) => sender.Send(new UpdateProjectCommand(projectId, request.Name, request.Description, request.DefaultLanguage, request.IsPublic), cancellationToken);

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken cancellationToken) { await sender.Send(new DeleteProjectCommand(projectId), cancellationToken); return NoContent(); }

    [HttpGet("{projectId:guid}/members")]
    public Task<IReadOnlyList<ProjectMemberDetails>> Members(Guid projectId, CancellationToken cancellationToken) => sender.Send(new ListProjectMembersQuery(projectId), cancellationToken);

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid projectId, Guid userId, CancellationToken cancellationToken) { await sender.Send(new RemoveProjectMemberCommand(projectId, userId), cancellationToken); return NoContent(); }

    [HttpPut("{projectId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid projectId, Guid userId, ChangeRoleRequest request, CancellationToken cancellationToken) { await sender.Send(new ChangeProjectMemberRoleCommand(projectId, userId, request.Role), cancellationToken); return NoContent(); }

    [HttpPost("{projectId:guid}/invitations")]
    [EnableRateLimiting("invitations")]
    public Task<CreatedInvitation> Invite(Guid projectId, InviteMemberRequest request, CancellationToken cancellationToken) => sender.Send(new InviteProjectMemberCommand(projectId, request.Email, request.Role), cancellationToken);

    [HttpGet("{projectId:guid}/invitations")]
    public Task<IReadOnlyList<ProjectInvitationDetails>> Invitations(Guid projectId, CancellationToken cancellationToken) => sender.Send(new ListPendingInvitationsQuery(projectId), cancellationToken);

    [HttpPost("invitations/accept")]
    [EnableRateLimiting("invitations")]
    public async Task<ActionResult<object>> Accept(InvitationTokenRequest request, CancellationToken cancellationToken) => Ok(new { ProjectId = await sender.Send(new AcceptProjectInvitationCommand(request.Token), cancellationToken) });

    [HttpPost("invitations/reject")]
    [EnableRateLimiting("invitations")]
    public async Task<IActionResult> Reject(InvitationTokenRequest request, CancellationToken cancellationToken) { await sender.Send(new RejectProjectInvitationCommand(request.Token), cancellationToken); return NoContent(); }

    [HttpPost("invitations/{invitationId:guid}/accept")]
    [EnableRateLimiting("invitations")]
    public async Task<ActionResult<object>> AcceptById(Guid invitationId, CancellationToken cancellationToken) =>
        Ok(new { ProjectId = await sender.Send(new AcceptProjectInvitationByIdCommand(invitationId), cancellationToken) });

    [HttpPost("invitations/{invitationId:guid}/reject")]
    [EnableRateLimiting("invitations")]
    public async Task<IActionResult> RejectById(Guid invitationId, CancellationToken cancellationToken)
    {
        await sender.Send(new RejectProjectInvitationByIdCommand(invitationId), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateProjectRequest(string Name, string? Description, string DefaultLanguage, bool IsPublic);
public sealed record UpdateProjectRequest(string Name, string? Description, string DefaultLanguage, bool IsPublic);
public sealed record InviteMemberRequest(string Email, ProjectRole Role);
public sealed record ChangeRoleRequest(ProjectRole Role);
public sealed record InvitationTokenRequest(string Token);
