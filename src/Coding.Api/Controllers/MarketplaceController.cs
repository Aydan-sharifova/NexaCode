using System.Text.Json;
using Coding.Application.Features.Marketplace;
using Coding.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/marketplace")]
public sealed class MarketplaceController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<MarketplaceItemSummary>> List([FromQuery] MarketplaceCategory? category = null, [FromQuery] string? search = null, [FromQuery] int skip = 0, [FromQuery] int take = 30, CancellationToken ct = default) => sender.Send(new ListMarketplaceItemsQuery(category, search, Skip: skip, Take: take), ct);
    [HttpGet("mine")]
    public Task<IReadOnlyList<MarketplaceItemSummary>> Mine([FromQuery] int skip = 0, [FromQuery] int take = 30, CancellationToken ct = default) => sender.Send(new ListMarketplaceItemsQuery(null, null, Mine: true, Skip: skip, Take: take), ct);
    [HttpGet("saved")]
    public Task<IReadOnlyList<MarketplaceItemSummary>> Saved([FromQuery] int skip = 0, [FromQuery] int take = 30, CancellationToken ct = default) => sender.Send(new ListMarketplaceItemsQuery(null, null, Saved: true, Skip: skip, Take: take), ct);
    [HttpGet("{slug}")]
    public Task<MarketplaceItemDetails> Details(string slug, CancellationToken ct) => sender.Send(new GetMarketplaceItemQuery(slug), ct);
    [HttpPost]
    public async Task<ActionResult<MarketplaceItemDetails>> Create(CreateMarketplaceItemRequest r, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await sender.Send(new CreateMarketplaceItemCommand(r.Title, r.Description, r.Category, r.Tags, r.Version, r.Manifest, r.Permissions, r.Changelog), ct));
    [HttpPost("{itemId:guid}/versions")]
    public Task<MarketplaceItemDetails> AddVersion(Guid itemId, AddMarketplaceVersionRequest r, CancellationToken ct) => sender.Send(new AddMarketplaceVersionCommand(itemId, r.Version, r.Manifest, r.Permissions, r.Changelog), ct);
    [HttpPost("{itemId:guid}/versions/{versionId:guid}/publish")]
    public Task<MarketplaceItemDetails> Publish(Guid itemId, Guid versionId, CancellationToken ct) => sender.Send(new PublishMarketplaceVersionCommand(itemId, versionId), ct);
    [HttpPut("{itemId:guid}/like")]
    public Task<MarketplaceItemSummary> Like(Guid itemId, ToggleMarketplaceRequest r, CancellationToken ct) => sender.Send(new SetMarketplaceLikeCommand(itemId, r.Enabled), ct);
    [HttpPut("{itemId:guid}/save")]
    public Task<MarketplaceItemSummary> Save(Guid itemId, ToggleMarketplaceRequest r, CancellationToken ct) => sender.Send(new SetMarketplaceSavedCommand(itemId, r.Enabled), ct);
}

[ApiController, Authorize, Route("api/projects/{projectId:guid}/marketplace/installations")]
public sealed class ProjectMarketplaceController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<MarketplaceInstallationItem>> List(Guid projectId, CancellationToken ct) => sender.Send(new ListMarketplaceInstallationsQuery(projectId), ct);
    [HttpPost]
    public async Task<ActionResult<MarketplaceInstallationItem>> Install(Guid projectId, InstallMarketplaceRequest r, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await sender.Send(new InstallMarketplaceAgentCommand(projectId, r.ItemId, r.VersionId, r.ApprovedDangerousPermissions), ct));
    [HttpPut("{installationId:guid}/status")]
    public Task<MarketplaceInstallationItem> Status(Guid projectId, Guid installationId, SetMarketplaceStatusRequest r, CancellationToken ct) => sender.Send(new SetMarketplaceInstallationStatusCommand(projectId, installationId, r.Status), ct);
    [HttpDelete("{installationId:guid}")]
    public async Task<IActionResult> Uninstall(Guid projectId, Guid installationId, CancellationToken ct) { await sender.Send(new UninstallMarketplaceItemCommand(projectId, installationId), ct); return NoContent(); }
}

public sealed record CreateMarketplaceItemRequest(string Title, string Description, MarketplaceCategory Category, IReadOnlyList<string> Tags, string Version, JsonElement Manifest, IReadOnlyList<string> Permissions, string? Changelog);
public sealed record AddMarketplaceVersionRequest(string Version, JsonElement Manifest, IReadOnlyList<string> Permissions, string? Changelog);
public sealed record ToggleMarketplaceRequest(bool Enabled);
public sealed record InstallMarketplaceRequest(Guid ItemId, Guid VersionId, IReadOnlyList<string> ApprovedDangerousPermissions);
public sealed record SetMarketplaceStatusRequest(MarketplaceInstallationStatus Status);
