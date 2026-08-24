using Coding.Application.Features.AiAgent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, EnableRateLimiting("ai"), Route("api/ai/approvals")]
public sealed class AiApprovalsController(IAiApprovalService approvals) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<AiApprovalDetails>> List([FromQuery] Guid? projectId, CancellationToken ct) => approvals.ListAsync(projectId, ct);
    [HttpGet("{id:guid}")] public Task<AiApprovalDetails> Get(Guid id, CancellationToken ct) => approvals.GetAsync(id, ct);
    [HttpPost("{id:guid}/approve")] public Task<AiApprovalDetails> Approve(Guid id, CancellationToken ct) => approvals.ApproveAsync(id, ct);
    [HttpPost("{id:guid}/reject")] public Task<AiApprovalDetails> Reject(Guid id, CancellationToken ct) => approvals.RejectAsync(id, ct);
}
