using Coding.Application.Abstractions;
using Coding.Application.Features.Runtime;
using Coding.Data;
using Coding.Exceptions;
using Coding.Enums;
using Coding.Domain.Services;
using Coding.Application.Features.Debugging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, EnableRateLimiting("ai"), Route("api/projects/{projectId:guid}/execution")]
public sealed class CodeExecutionController(
    AppDbContext db,
    ICurrentUser currentUser,
    IRuntimeProvider runtime,
    IDebuggingTimelineService debugging) : ControllerBase
{
    private const int MaximumSourceCharacters = 100_000;

    [HttpPost("csharp")]
    public async Task<ActionResult<CodeExecutionResponse>> RunCSharp(
        Guid projectId,
        CodeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var access = await db.ProjectMembers.AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUser.UserId)
            .Select(member => new { member.Role, member.Project.Status, member.Project.DeadlineAt })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException("You are not a member of this project.");
        var status = ProjectLifecycle.EffectiveStatus(access.Status, access.DeadlineAt, DateTime.UtcNow);
        if (ProjectLifecycle.IsWorkspaceReadOnly(access.Role, status))
            throw new ForbiddenException("The current project role or status does not permit code execution.");

        var source = request.Source?.Trim() ?? string.Empty;
        if (source.Length is 0 or > MaximumSourceCharacters)
            return BadRequest($"C# source must contain 1 to {MaximumSourceCharacters:N0} characters.");

        try
        {
            var result = await runtime.ExecuteAsync(
                new RuntimeExecutionRequest("csharp", source, request.TimeoutSeconds ?? 8),
                cancellationToken);
            if (request.WorkspaceNodeId.HasValue && !await db.WorkspaceNodes.AsNoTracking().AnyAsync(x => x.ID == request.WorkspaceNodeId && x.ProjectId == projectId && x.NodeType == WorkspaceNodeType.File, cancellationToken))
                return BadRequest("The selected workspace file does not belong to this project.");
            var incidentId = await debugging.CaptureFailureAsync(new DebugExecutionCapture(projectId, currentUser.UserId, request.WorkspaceNodeId, DebuggingIncidentKind.Runtime, "csharp", source, result.ExitCode, result.Stdout, result.Stderr, result.TimedOut, result.DurationMs), cancellationToken);
            return Ok(new CodeExecutionResponse(result.ExitCode, result.Stdout, result.Stderr, result.TimedOut, result.DurationMs, incidentId));
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, exception.Message);
        }
    }
}

public sealed record CodeExecutionRequest(string Source, int? TimeoutSeconds = null, Guid? WorkspaceNodeId = null);
public sealed record CodeExecutionResponse(int? ExitCode, string Stdout, string Stderr, bool TimedOut, int DurationMs, Guid? DebuggingIncidentId);
