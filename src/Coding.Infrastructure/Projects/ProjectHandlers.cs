using System.Security.Cryptography;
using System.Text;
using Coding.Application.Abstractions;
using Coding.Application.Features.Projects;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Models;
using MediatR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Coding.Application.Features.Notifications;
using Coding.Infrastructure.Authentication;
using Coding.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Coding.Infrastructure.Projects;

internal static class ProjectAccess
{
    public static async Task<ProjectRole> RequireMemberAsync(AppDbContext context, Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var role = await context.ProjectMembers.AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == userId)
            .Select(member => (ProjectRole?)member.Role).SingleOrDefaultAsync(cancellationToken);
        return role ?? throw new ForbiddenException("You are not a member of this project.");
    }

    public static void RequireManager(ProjectRole role)
    {
        if (role is not (ProjectRole.Owner or ProjectRole.Admin))
            throw new ForbiddenException("Project management access is required.");
    }

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class CreateProjectHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<CreateProjectCommand, ProjectDetails>
{
    public async Task<ProjectDetails> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var project = new Project { ID = Guid.NewGuid(), Name = request.Name.Trim(), Description = request.Description?.Trim(), DefaultLanguage = request.DefaultLanguage.Trim(), IsPublic = request.IsPublic, OwnerId = currentUser.UserId, CreatedAt = now, CreatAt = now };
            project.Members.Add(new ProjectMember { ID = Guid.NewGuid(), Project = project, UserId = currentUser.UserId, Role = ProjectRole.Owner, JoinedAt = now, CreatAt = now });
            context.Conversations.Add(new Conversation { ID = Guid.NewGuid(), Type = ConversationType.ProjectChannel, Project = project, Name = project.Name, CreatedAt = now, UpdatedAt = now, Participants = [new ConversationParticipant { ID = Guid.NewGuid(), UserId = currentUser.UserId, JoinedAt = now }] });
            context.Projects.Add(project);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProjectDetails(project.ID, project.Name, project.Description, project.DefaultLanguage, project.IsPublic, project.OwnerId, ProjectRole.Owner, project.CreatedAt, project.UpdateAt);
        });
    }
}

public sealed class UpdateProjectHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<UpdateProjectCommand, ProjectDetails>
{
    public async Task<ProjectDetails> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(context, request.ProjectId, currentUser.UserId, cancellationToken);
        ProjectAccess.RequireManager(role);
        var project = await context.Projects.SingleOrDefaultAsync(item => item.ID == request.ProjectId, cancellationToken) ?? throw new NotFoundException("Project not found.");
        project.Name = request.Name.Trim(); project.Description = request.Description?.Trim(); project.DefaultLanguage = request.DefaultLanguage.Trim(); project.IsPublic = request.IsPublic; project.UpdateAt = DateTime.UtcNow;
        var channel = await context.Conversations.SingleOrDefaultAsync(item => item.ProjectId == request.ProjectId, cancellationToken);
        if (channel is not null) { channel.Name = project.Name; channel.UpdatedAt = project.UpdateAt.Value; }
        await context.SaveChangesAsync(cancellationToken);
        return new(project.ID, project.Name, project.Description, project.DefaultLanguage, project.IsPublic, project.OwnerId, role, project.CreatedAt, project.UpdateAt);
    }
}

public sealed class DeleteProjectHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<DeleteProjectCommand>
{
    public async Task Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(context, request.ProjectId, currentUser.UserId, cancellationToken);
        if (role != ProjectRole.Owner) throw new ForbiddenException("Only the project owner can delete this project.");
        var project = await context.Projects.SingleOrDefaultAsync(item => item.ID == request.ProjectId, cancellationToken) ?? throw new NotFoundException("Project not found.");
        project.IsDeleted = true; project.DeletedAt = project.UpdateAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class InviteProjectMemberHandler(
    AppDbContext context,
    ICurrentUser currentUser,
    INotificationService notifications,
    IEmailSender emailSender,
    IOptions<SmtpSettings> smtpOptions,
    ILogger<InviteProjectMemberHandler> logger) : IRequestHandler<InviteProjectMemberCommand, CreatedInvitation>
{
    public async Task<CreatedInvitation> Handle(InviteProjectMemberCommand request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(context, request.ProjectId, currentUser.UserId, cancellationToken);
        ProjectAccess.RequireManager(role);
        var email = request.Email.Trim().ToLowerInvariant();
        var existingUserId = await context.Users.Where(user => user.Email.ToLower() == email).Select(user => (Guid?)user.ID).SingleOrDefaultAsync(cancellationToken);
        if (existingUserId.HasValue && await context.ProjectMembers.AnyAsync(member => member.ProjectId == request.ProjectId && member.UserId == existingUserId.Value, cancellationToken))
            throw new ConflictException("This user is already a project member.");
        var hasActiveInvitation = await context.ProjectInvitations.AnyAsync(invitation => invitation.ProjectId == request.ProjectId && invitation.Email == email && invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (hasActiveInvitation) throw new ConflictException("An active invitation already exists for this email.");
        await context.ProjectInvitations.Where(invitation => invitation.ProjectId == request.ProjectId && invitation.Email == email && invitation.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(invitation => invitation.Status, InvitationStatus.Expired), cancellationToken);
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        var invitation = new ProjectInvitation { ID = Guid.NewGuid(), ProjectId = request.ProjectId, Email = email, Role = request.Role, TokenHash = ProjectAccess.HashToken(token), Status = InvitationStatus.Pending, ExpiresAt = DateTime.UtcNow.AddDays(7), InvitedById = currentUser.UserId };
        context.ProjectInvitations.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);
        var projectDetails = await context.Projects.Where(project => project.ID == request.ProjectId)
            .Select(project => new
            {
                project.Name,
                InviterName = project.Members.Where(member => member.UserId == currentUser.UserId)
                    .Select(member => member.User.FirstName + " " + member.User.LastName)
                    .Single()
            })
            .SingleAsync(cancellationToken);
        if (existingUserId.HasValue && existingUserId.Value != currentUser.UserId)
        {
            await notifications.CreateAsync(new CreateNotificationRequest(existingUserId.Value, NotificationType.Invitation, "Project invitation", $"You were invited to {projectDetails.Name}.", invitation.ID, nameof(ProjectInvitation)), cancellationToken);
        }
        try
        {
            var link = $"{smtpOptions.Value.ClientBaseUrl.TrimEnd('/')}/invitations/{Uri.EscapeDataString(token)}";
            await emailSender.SendAsync(email, $"Invitation to {projectDetails.Name}",
                AccountEmailTemplates.ProjectInvitation(projectDetails.InviterName.Trim(), projectDetails.Name,
                    request.Role.ToString(), invitation.ExpiresAt, link), cancellationToken);
        }
        catch (Exception error) when (error is EmailDeliveryException && error is not OperationCanceledException)
        {
            logger.LogError(error, "Project invitation {InvitationId} was saved, but its email was not delivered.", invitation.ID);
        }
        return new(invitation.ID, token, invitation.ExpiresAt);
    }
}

public sealed class AcceptProjectInvitationHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<AcceptProjectInvitationCommand, Guid>
{
    public async Task<Guid> Handle(AcceptProjectInvitationCommand request, CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var hash = ProjectAccess.HashToken(request.Token);
            var invitation = await context.ProjectInvitations.Include(item => item.Project).SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken) ?? throw new NotFoundException("Invitation not found.");
            if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt <= DateTime.UtcNow) throw new ConflictException("This invitation is no longer active.");
            if (!string.Equals(invitation.Email, currentUser.Email, StringComparison.OrdinalIgnoreCase)) throw new ForbiddenException("This invitation belongs to another email address.");
            if (await context.ProjectMembers.AnyAsync(member => member.ProjectId == invitation.ProjectId && member.UserId == currentUser.UserId, cancellationToken)) throw new ConflictException("You are already a project member.");
            context.ProjectMembers.Add(new ProjectMember { ID = Guid.NewGuid(), ProjectId = invitation.ProjectId, UserId = currentUser.UserId, Role = invitation.Role, JoinedAt = DateTime.UtcNow });
            var channelId = await context.Conversations.Where(item => item.ProjectId == invitation.ProjectId).Select(item => (Guid?)item.ID).SingleOrDefaultAsync(cancellationToken);
            if (channelId.HasValue) context.ConversationParticipants.Add(new ConversationParticipant { ID = Guid.NewGuid(), ConversationId = channelId.Value, UserId = currentUser.UserId, JoinedAt = DateTime.UtcNow });
            invitation.Status = InvitationStatus.Accepted; invitation.RespondedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return invitation.ProjectId;
        });
    }
}

public sealed class RejectProjectInvitationHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<RejectProjectInvitationCommand>
{
    public async Task Handle(RejectProjectInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await context.ProjectInvitations.SingleOrDefaultAsync(item => item.TokenHash == ProjectAccess.HashToken(request.Token), cancellationToken) ?? throw new NotFoundException("Invitation not found.");
        if (!string.Equals(invitation.Email, currentUser.Email, StringComparison.OrdinalIgnoreCase)) throw new ForbiddenException("This invitation belongs to another email address.");
        if (invitation.Status != InvitationStatus.Pending) throw new ConflictException("This invitation is no longer active.");
        invitation.Status = InvitationStatus.Rejected; invitation.RespondedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AcceptProjectInvitationByIdHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<AcceptProjectInvitationByIdCommand, Guid>
{
    public async Task<Guid> Handle(AcceptProjectInvitationByIdCommand request, CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var invitation = await context.ProjectInvitations.Include(item => item.Project)
                .SingleOrDefaultAsync(item => item.ID == request.InvitationId, cancellationToken)
                ?? throw new NotFoundException("Invitation not found.");
            EnsureInvitationBelongsToCurrentUser(invitation, currentUser);
            if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt <= DateTime.UtcNow)
                throw new ConflictException("This invitation is no longer active.");
            if (await context.ProjectMembers.AnyAsync(member => member.ProjectId == invitation.ProjectId && member.UserId == currentUser.UserId, cancellationToken))
                throw new ConflictException("You are already a project member.");
            context.ProjectMembers.Add(new ProjectMember { ID = Guid.NewGuid(), ProjectId = invitation.ProjectId, UserId = currentUser.UserId, Role = invitation.Role, JoinedAt = DateTime.UtcNow });
            var channelId = await context.Conversations.Where(item => item.ProjectId == invitation.ProjectId).Select(item => (Guid?)item.ID).SingleOrDefaultAsync(cancellationToken);
            if (channelId.HasValue)
                context.ConversationParticipants.Add(new ConversationParticipant { ID = Guid.NewGuid(), ConversationId = channelId.Value, UserId = currentUser.UserId, JoinedAt = DateTime.UtcNow });
            invitation.Status = InvitationStatus.Accepted;
            invitation.RespondedAt = DateTime.UtcNow;
            await context.Notifications.Where(notification => notification.UserId == currentUser.UserId && notification.RelatedEntityId == invitation.ID && !notification.IsRead)
                .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.IsRead, true).SetProperty(notification => notification.ReadAt, DateTime.UtcNow), cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return invitation.ProjectId;
        });
    }

    internal static void EnsureInvitationBelongsToCurrentUser(ProjectInvitation invitation, ICurrentUser currentUser)
    {
        if (!string.Equals(invitation.Email, currentUser.Email, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("This invitation belongs to another email address.");
    }
}

public sealed class RejectProjectInvitationByIdHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<RejectProjectInvitationByIdCommand>
{
    public async Task Handle(RejectProjectInvitationByIdCommand request, CancellationToken cancellationToken)
    {
        var invitation = await context.ProjectInvitations.SingleOrDefaultAsync(item => item.ID == request.InvitationId, cancellationToken)
            ?? throw new NotFoundException("Invitation not found.");
        AcceptProjectInvitationByIdHandler.EnsureInvitationBelongsToCurrentUser(invitation, currentUser);
        if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt <= DateTime.UtcNow)
            throw new ConflictException("This invitation is no longer active.");
        invitation.Status = InvitationStatus.Rejected;
        invitation.RespondedAt = DateTime.UtcNow;
        await context.Notifications.Where(notification => notification.UserId == currentUser.UserId && notification.RelatedEntityId == invitation.ID && !notification.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.IsRead, true).SetProperty(notification => notification.ReadAt, DateTime.UtcNow), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ChangeProjectMemberRoleHandler(AppDbContext context, ICurrentUser currentUser, INotificationService notifications) : IRequestHandler<ChangeProjectMemberRoleCommand>
{
    public async Task Handle(ChangeProjectMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var actorRole = await ProjectAccess.RequireMemberAsync(context, request.ProjectId, currentUser.UserId, cancellationToken);
        if (actorRole != ProjectRole.Owner) throw new ForbiddenException("Only the project owner can change member roles.");
        var member = await context.ProjectMembers.SingleOrDefaultAsync(item => item.ProjectId == request.ProjectId && item.UserId == request.UserId, cancellationToken) ?? throw new NotFoundException("Project member not found.");
        if (member.Role == ProjectRole.Owner) throw new ForbiddenException("The owner role cannot be changed.");
        member.Role = request.Role; member.UpdateAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        if (request.UserId != currentUser.UserId)
        {
            var projectName = await context.Projects.Where(project => project.ID == request.ProjectId).Select(project => project.Name).SingleAsync(cancellationToken);
            await notifications.CreateAsync(new CreateNotificationRequest(request.UserId, NotificationType.RoleChange, "Project role changed", $"Your role in {projectName} is now {request.Role}.", request.ProjectId, nameof(Project)), cancellationToken);
        }
    }
}

public sealed class RemoveProjectMemberHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<RemoveProjectMemberCommand>
{
    public async Task Handle(RemoveProjectMemberCommand request, CancellationToken cancellationToken)
    {
        var actorRole = await ProjectAccess.RequireMemberAsync(context, request.ProjectId, currentUser.UserId, cancellationToken);
        if (actorRole != ProjectRole.Owner) throw new ForbiddenException("Only the project owner can remove members.");
        var member = await context.ProjectMembers.SingleOrDefaultAsync(item => item.ProjectId == request.ProjectId && item.UserId == request.UserId, cancellationToken) ?? throw new NotFoundException("Project member not found.");
        if (member.Role == ProjectRole.Owner) throw new ForbiddenException("The project owner cannot be removed.");
        var channelParticipant = await context.ConversationParticipants.SingleOrDefaultAsync(item => item.Conversation.ProjectId == request.ProjectId && item.UserId == request.UserId, cancellationToken);
        if (channelParticipant is not null) context.ConversationParticipants.Remove(channelParticipant);
        context.ProjectMembers.Remove(member); await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ListMyProjectsHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<ListMyProjectsQuery, IReadOnlyList<ProjectListItem>>
{
    public async Task<IReadOnlyList<ProjectListItem>> Handle(ListMyProjectsQuery request, CancellationToken cancellationToken) =>
        await context.ProjectMembers.AsNoTracking().Where(member => member.UserId == currentUser.UserId)
            .OrderByDescending(member => member.Project.UpdateAt ?? member.Project.CreatedAt)
            .Select(member => new ProjectListItem(member.ProjectId, member.Project.Name, member.Project.Description, member.Project.DefaultLanguage, member.Role, member.Project.Members.Count, member.Project.CreatedAt)).ToListAsync(cancellationToken);
}

public sealed class GetProjectDetailsHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<GetProjectDetailsQuery, ProjectDetails>
{
    public async Task<ProjectDetails> Handle(GetProjectDetailsQuery request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(context, request.ProjectId, currentUser.UserId, cancellationToken);
        return await context.Projects.AsNoTracking().Where(project => project.ID == request.ProjectId)
            .Select(project => new ProjectDetails(project.ID, project.Name, project.Description, project.DefaultLanguage, project.IsPublic, project.OwnerId, role, project.CreatedAt, project.UpdateAt))
            .SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Project not found.");
    }
}

public sealed class ListProjectMembersHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<ListProjectMembersQuery, IReadOnlyList<ProjectMemberDetails>>
{
    public async Task<IReadOnlyList<ProjectMemberDetails>> Handle(ListProjectMembersQuery request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(context, request.ProjectId, currentUser.UserId, cancellationToken);
        return await context.ProjectMembers.AsNoTracking().Where(member => member.ProjectId == request.ProjectId).OrderBy(member => member.Role)
            .Select(member => new ProjectMemberDetails(member.UserId, member.User.FirstName + " " + member.User.LastName, member.User.Email, member.User.AvatarUrl, member.Role, member.JoinedAt)).ToListAsync(cancellationToken);
    }
}

public sealed class ListPendingInvitationsHandler(AppDbContext context, ICurrentUser currentUser) : IRequestHandler<ListPendingInvitationsQuery, IReadOnlyList<ProjectInvitationDetails>>
{
    public async Task<IReadOnlyList<ProjectInvitationDetails>> Handle(ListPendingInvitationsQuery request, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(context, request.ProjectId, currentUser.UserId, cancellationToken); ProjectAccess.RequireManager(role);
        return await context.ProjectInvitations.AsNoTracking().Where(invitation => invitation.ProjectId == request.ProjectId && invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt > DateTime.UtcNow)
            .Select(invitation => new ProjectInvitationDetails(invitation.ID, invitation.Email, invitation.Role, invitation.ExpiresAt, invitation.InvitedBy.FirstName + " " + invitation.InvitedBy.LastName)).ToListAsync(cancellationToken);
    }
}
