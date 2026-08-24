using System.Text.Json;
using Coding.Application.Features.AiAgent;
using Coding.Data;
using Coding.Enums;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.AiAgent.Tools;

/// <summary>
/// Returns the project member roster with roles. Read-only.
/// </summary>
public sealed class GetProjectMembersTool(AppDbContext db) : IAiTool
{
    public static readonly AiToolDescriptor StaticDescriptor = new(
        Name: "get_project_members",
        Description: "Returns the project member roster with roles. Read-only.",
        RiskLevel: AiToolRiskLevel.ReadOnly,
        AllowedModes: new HashSet<AiAgentMode> { AiAgentMode.Ask, AiAgentMode.Plan, AiAgentMode.Agent, AiAgentMode.Review },
        RequiredRoles: new HashSet<ProjectRole> { ProjectRole.Owner, ProjectRole.Admin, ProjectRole.Maintainer, ProjectRole.Developer, ProjectRole.Viewer },
        InputType: typeof(GetProjectMembersInput));

    public AiToolDescriptor Descriptor => StaticDescriptor;

    public async Task<IAiToolResult> ExecuteAsync(JsonElement arguments, AiAgentRun run, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, run.ProjectId, run.UserId, cancellationToken);
        var members = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == run.ProjectId)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .Select(m => new
            {
                userId = m.UserId,
                userName = m.User.UserName,
                fullName = m.User.FirstName + " " + m.User.LastName,
                role = m.Role.ToString(),
                joinedAt = m.JoinedAt
            })
            .ToListAsync(cancellationToken);
        var json = JsonSerializer.Serialize(new { members });
        return new AiReadToolGuard.AiTextResult($"{members.Count} members", json);
    }
}

public sealed record GetProjectMembersInput();