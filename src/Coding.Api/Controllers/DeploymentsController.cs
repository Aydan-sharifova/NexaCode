using Coding.Application.Features.Deployments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Route("api/projects/{projectId:guid}/deployments"), Authorize]
public sealed class ProjectDeploymentsController(IProjectDeploymentService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DeploymentSummary>> List(Guid projectId, CancellationToken ct) => service.ListAsync(projectId, ct);

    [HttpPost, EnableRateLimiting("runtime")]
    public Task<DeploymentSummary> Deploy(Guid projectId, CancellationToken ct) => service.DeployAsync(projectId, ct);
}

[ApiController, Route("deploy")]
public sealed class PublicDeploymentsController(IProjectDeploymentService service) : ControllerBase
{
    [HttpGet("{slug}")]
    public IActionResult RedirectToRoot(string slug) => Redirect($"/deploy/{Uri.EscapeDataString(slug)}/");

    [HttpGet("{slug}/{**path}")]
    public async Task<IActionResult> Asset(string slug, string? path, CancellationToken ct)
    {
        var asset = await service.GetPublicAssetAsync(slug, path, ct);
        if (asset is null) return NotFound();
        Response.Headers.CacheControl = "public,max-age=60";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'none'; frame-ancestors 'self'; base-uri 'self'; form-action 'none'";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Content(asset.Content, asset.ContentType);
    }
}
