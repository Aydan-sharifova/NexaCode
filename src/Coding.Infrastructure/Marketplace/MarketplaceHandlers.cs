using System.Text.Json;
using System.Text.RegularExpressions;
using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Marketplace;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Marketplace;

internal static partial class MarketplaceSupport
{
    public static string[] Strings(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static MarketplaceAuthor Author(User user) => new(user.ID, user.PublicId, user.UserName, $"{user.FirstName} {user.LastName}".Trim(), user.AvatarUrl);

    public static MarketplaceVersionItem Version(MarketplaceItemVersion version) => new(version.ID, version.Version,
        Strings(version.PermissionsJson), version.Changelog, version.Checksum, version.IsPublished, version.CreatAt, version.PublishedAt);

    public static MarketplaceItemSummary Summary(MarketplaceItem item, Guid userId, bool liked, bool saved)
    {
        var latest = item.Versions.Where(version => version.IsPublished).OrderByDescending(version => version.PublishedAt).FirstOrDefault()
            ?? (item.AuthorId == userId ? item.Versions.OrderByDescending(version => version.CreatAt).FirstOrDefault() : null);
        return new(item.ID, item.Slug, item.Title, item.Description, item.Category, item.Status, Author(item.Author), Strings(item.TagsJson),
            item.DownloadCount, item.LikeCount, liked, saved, latest is null ? null : Version(latest), item.UpdatedAt);
    }

    public static async Task<MarketplaceItem> Item(AppDbContext db, Guid id, CancellationToken ct) =>
        await db.MarketplaceItems.Include(item => item.Author).Include(item => item.Versions).SingleOrDefaultAsync(item => item.ID == id, ct)
        ?? throw new NotFoundException("Marketplace item not found.");

    public static async Task<MarketplaceItemDetails> Details(AppDbContext db, MarketplaceItem item, Guid userId, CancellationToken ct)
    {
        var liked = await db.MarketplaceLikes.AnyAsync(value => value.MarketplaceItemId == item.ID && value.UserId == userId, ct);
        var saved = await db.SavedMarketplaceItems.AnyAsync(value => value.MarketplaceItemId == item.ID && value.UserId == userId, ct);
        var versions = item.Versions.Where(version => version.IsPublished || item.AuthorId == userId).OrderByDescending(version => version.CreatAt).Select(Version).ToArray();
        var selected = item.Versions.Where(version => version.IsPublished || item.AuthorId == userId).OrderByDescending(version => version.PublishedAt ?? version.CreatAt).FirstOrDefault();
        JsonElement? manifest = selected is null ? null : JsonSerializer.Deserialize<JsonElement>(selected.ManifestJson);
        return new(Summary(item, userId, liked, saved), versions, manifest, item.AuthorId == userId);
    }

    public static void RequirePublished(MarketplaceItem item)
    {
        if (item.Status != MarketplaceItemStatus.Published) throw new ConflictException("Marketplace item is not published.");
    }

    public static string BaseSlug(string title)
    {
        var slug = NonSlug().Replace(title.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "package" : slug[..Math.Min(slug.Length, 120)];
    }

    public static async Task<string> UniqueSlug(AppDbContext db, string title, CancellationToken ct)
    {
        var root = BaseSlug(title); var slug = root; var suffix = 1;
        while (await db.MarketplaceItems.IgnoreQueryFilters().AnyAsync(item => item.Slug == slug, ct)) slug = $"{root}-{++suffix}";
        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();
}

public sealed class CreateMarketplaceItemHandler(AppDbContext db, ICurrentUser user, IMarketplaceManifestValidator validator, IActivityLogger activity)
    : IRequestHandler<CreateMarketplaceItemCommand, MarketplaceItemDetails>
{
    public async Task<MarketplaceItemDetails> Handle(CreateMarketplaceItemCommand r, CancellationToken ct)
    {
        var validated = validator.Validate(r.Category, r.Manifest, r.Permissions);
        var now = DateTime.UtcNow;
        var item = new MarketplaceItem { ID = Guid.NewGuid(), AuthorId = user.UserId, Slug = await MarketplaceSupport.UniqueSlug(db, r.Title, ct), Title = r.Title.Trim(), Description = r.Description.Trim(), Category = r.Category, TagsJson = JsonSerializer.Serialize(r.Tags.Select(tag => tag.Trim().ToLowerInvariant()).Distinct()), UpdatedAt = now };
        item.Versions.Add(new MarketplaceItemVersion { ID = Guid.NewGuid(), Version = r.Version, ManifestJson = validated.ManifestJson, PermissionsJson = validated.PermissionsJson, Checksum = validated.Checksum, Changelog = r.Changelog?.Trim() });
        db.MarketplaceItems.Add(item); await db.SaveChangesAsync(ct);
        await activity.LogAsync(new(user.UserId, null, "MarketplaceItemCreated", nameof(MarketplaceItem), item.ID, $"Created marketplace draft '{item.Title}'."), ct);
        item.Author = await db.Users.SingleAsync(value => value.ID == user.UserId, ct);
        return await MarketplaceSupport.Details(db, item, user.UserId, ct);
    }
}

public sealed class AddMarketplaceVersionHandler(AppDbContext db, ICurrentUser user, IMarketplaceManifestValidator validator)
    : IRequestHandler<AddMarketplaceVersionCommand, MarketplaceItemDetails>
{
    public async Task<MarketplaceItemDetails> Handle(AddMarketplaceVersionCommand r, CancellationToken ct)
    {
        var item = await MarketplaceSupport.Item(db, r.ItemId, ct);
        if (item.AuthorId != user.UserId) throw new ForbiddenException("Only the marketplace author can add versions.");
        if (item.Status == MarketplaceItemStatus.Suspended) throw new ConflictException("Suspended marketplace items cannot be changed.");
        if (item.Versions.Any(value => value.Version == r.Version)) throw new ConflictException("This marketplace version already exists.");
        var validated = validator.Validate(item.Category, r.Manifest, r.Permissions);
        item.Versions.Add(new MarketplaceItemVersion { ID = Guid.NewGuid(), Version = r.Version, ManifestJson = validated.ManifestJson, PermissionsJson = validated.PermissionsJson, Checksum = validated.Checksum, Changelog = r.Changelog?.Trim() });
        item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        return await MarketplaceSupport.Details(db, item, user.UserId, ct);
    }
}

public sealed class PublishMarketplaceVersionHandler(AppDbContext db, ICurrentUser user, IActivityLogger activity)
    : IRequestHandler<PublishMarketplaceVersionCommand, MarketplaceItemDetails>
{
    public async Task<MarketplaceItemDetails> Handle(PublishMarketplaceVersionCommand r, CancellationToken ct)
    {
        var item = await MarketplaceSupport.Item(db, r.ItemId, ct);
        if (item.AuthorId != user.UserId) throw new ForbiddenException("Only the marketplace author can publish versions.");
        if (item.Status == MarketplaceItemStatus.Suspended) throw new ConflictException("Suspended marketplace items cannot be published.");
        var version = item.Versions.SingleOrDefault(value => value.ID == r.VersionId) ?? throw new NotFoundException("Marketplace version not found.");
        if (!version.IsPublished) { version.IsPublished = true; version.PublishedAt = DateTime.UtcNow; }
        item.Status = MarketplaceItemStatus.Published; item.PublishedAt ??= version.PublishedAt; item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await activity.LogAsync(new(user.UserId, null, "MarketplaceVersionPublished", nameof(MarketplaceItemVersion), version.ID, $"Published {item.Title} {version.Version}."), ct);
        return await MarketplaceSupport.Details(db, item, user.UserId, ct);
    }
}

public sealed class GetMarketplaceItemHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetMarketplaceItemQuery, MarketplaceItemDetails>
{
    public async Task<MarketplaceItemDetails> Handle(GetMarketplaceItemQuery r, CancellationToken ct)
    {
        var item = await db.MarketplaceItems.Include(value => value.Author).Include(value => value.Versions).SingleOrDefaultAsync(value => value.Slug == r.Slug, ct) ?? throw new NotFoundException("Marketplace item not found.");
        if (item.Status != MarketplaceItemStatus.Published && item.AuthorId != user.UserId) throw new NotFoundException("Marketplace item not found.");
        return await MarketplaceSupport.Details(db, item, user.UserId, ct);
    }
}

public sealed class ListMarketplaceItemsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<ListMarketplaceItemsQuery, IReadOnlyList<MarketplaceItemSummary>>
{
    public async Task<IReadOnlyList<MarketplaceItemSummary>> Handle(ListMarketplaceItemsQuery r, CancellationToken ct)
    {
        var query = db.MarketplaceItems.Include(item => item.Author).Include(item => item.Versions).AsSplitQuery().AsQueryable();
        query = r.Mine ? query.Where(item => item.AuthorId == user.UserId) : query.Where(item => item.Status == MarketplaceItemStatus.Published);
        if (r.Saved) query = query.Where(item => db.SavedMarketplaceItems.Any(saved => saved.MarketplaceItemId == item.ID && saved.UserId == user.UserId));
        if (r.Category is not null) query = query.Where(item => item.Category == r.Category);
        if (!string.IsNullOrWhiteSpace(r.Search)) { var term = r.Search.Trim().ToLower(); query = query.Where(item => item.Title.ToLower().Contains(term) || item.Description.ToLower().Contains(term)); }
        var items = await query.OrderByDescending(item => item.UpdatedAt).Skip(Math.Max(0, r.Skip)).Take(Math.Clamp(r.Take, 1, 100)).ToListAsync(ct);
        var ids = items.Select(item => item.ID).ToArray();
        var liked = (await db.MarketplaceLikes.Where(value => value.UserId == user.UserId && ids.Contains(value.MarketplaceItemId)).Select(value => value.MarketplaceItemId).ToListAsync(ct)).ToHashSet();
        var savedIds = (await db.SavedMarketplaceItems.Where(value => value.UserId == user.UserId && ids.Contains(value.MarketplaceItemId)).Select(value => value.MarketplaceItemId).ToListAsync(ct)).ToHashSet();
        return items.Select(item => MarketplaceSupport.Summary(item, user.UserId, liked.Contains(item.ID), savedIds.Contains(item.ID))).ToArray();
    }
}

public sealed class SetMarketplaceLikeHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<SetMarketplaceLikeCommand, MarketplaceItemSummary>
{
    public async Task<MarketplaceItemSummary> Handle(SetMarketplaceLikeCommand r, CancellationToken ct)
    {
        var item = await MarketplaceSupport.Item(db, r.ItemId, ct); MarketplaceSupport.RequirePublished(item);
        var existing = await db.MarketplaceLikes.SingleOrDefaultAsync(value => value.MarketplaceItemId == r.ItemId && value.UserId == user.UserId, ct);
        if (r.Liked && existing is null) { db.MarketplaceLikes.Add(new() { MarketplaceItemId = r.ItemId, UserId = user.UserId }); item.LikeCount++; }
        else if (!r.Liked && existing is not null) { db.MarketplaceLikes.Remove(existing); item.LikeCount = Math.Max(0, item.LikeCount - 1); }
        await db.SaveChangesAsync(ct); return MarketplaceSupport.Summary(item, user.UserId, r.Liked, await db.SavedMarketplaceItems.AnyAsync(value => value.MarketplaceItemId == r.ItemId && value.UserId == user.UserId, ct));
    }
}

public sealed class SetMarketplaceSavedHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<SetMarketplaceSavedCommand, MarketplaceItemSummary>
{
    public async Task<MarketplaceItemSummary> Handle(SetMarketplaceSavedCommand r, CancellationToken ct)
    {
        var item = await MarketplaceSupport.Item(db, r.ItemId, ct); MarketplaceSupport.RequirePublished(item);
        var existing = await db.SavedMarketplaceItems.SingleOrDefaultAsync(value => value.MarketplaceItemId == r.ItemId && value.UserId == user.UserId, ct);
        if (r.Saved && existing is null) db.SavedMarketplaceItems.Add(new() { MarketplaceItemId = r.ItemId, UserId = user.UserId });
        else if (!r.Saved && existing is not null) db.SavedMarketplaceItems.Remove(existing);
        await db.SaveChangesAsync(ct); return MarketplaceSupport.Summary(item, user.UserId, await db.MarketplaceLikes.AnyAsync(value => value.MarketplaceItemId == r.ItemId && value.UserId == user.UserId, ct), r.Saved);
    }
}

public sealed class InstallMarketplaceAgentHandler(AppDbContext db, ICurrentUser user, IActivityLogger activity)
    : IRequestHandler<InstallMarketplaceAgentCommand, MarketplaceInstallationItem>
{
    public async Task<MarketplaceInstallationItem> Handle(InstallMarketplaceAgentCommand r, CancellationToken ct)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, r.ProjectId, user.UserId, ct); ProjectAccess.RequireManager(role);
        await ProjectAccess.EnsureWorkspaceWritableAsync(db, r.ProjectId, role, ct);
        var item = await MarketplaceSupport.Item(db, r.ItemId, ct); MarketplaceSupport.RequirePublished(item);
        if (item.Category != MarketplaceCategory.AiAgent) throw new ConflictException("Only AI agent packages currently have a sandboxed installation runtime.");
        var version = item.Versions.SingleOrDefault(value => value.ID == r.VersionId && value.IsPublished) ?? throw new NotFoundException("Published marketplace version not found.");
        var requested = MarketplaceSupport.Strings(version.PermissionsJson).ToHashSet(StringComparer.Ordinal);
        var approvals = r.ApprovedDangerousPermissions.Select(value => value.Trim().ToLowerInvariant()).Distinct().ToArray();
        if (approvals.Any(value => !MarketplacePermissions.Dangerous.Contains(value) || !requested.Contains(value))) throw new ConflictException("Only requested dangerous permissions can be approved.");
        var missing = requested.Where(MarketplacePermissions.Dangerous.Contains).Except(approvals).ToArray();
        if (missing.Length > 0) throw new ConflictException("Explicit approval is required for: " + string.Join(", ", missing));
        var installation = await db.MarketplaceInstallations.Include(value => value.MarketplaceItem).ThenInclude(value => value.Author).Include(value => value.MarketplaceItem).ThenInclude(value => value.Versions).Include(value => value.MarketplaceItemVersion)
            .SingleOrDefaultAsync(value => value.ProjectId == r.ProjectId && value.MarketplaceItemId == r.ItemId, ct);
        var isNew = installation is null;
        if (installation is null)
        {
            installation = new() { ID = Guid.NewGuid(), ProjectId = r.ProjectId, MarketplaceItemId = item.ID, MarketplaceItemVersionId = version.ID, InstalledById = user.UserId, ApprovedPermissionsJson = JsonSerializer.Serialize(approvals), InstalledAt = DateTime.UtcNow };
            db.MarketplaceInstallations.Add(installation); item.DownloadCount++;
        }
        else
        {
            installation.MarketplaceItemVersionId = version.ID; installation.MarketplaceItemVersion = version; installation.InstalledById = user.UserId;
            installation.ApprovedPermissionsJson = JsonSerializer.Serialize(approvals); installation.Status = MarketplaceInstallationStatus.Active; installation.DisabledAt = null;
        }
        await db.SaveChangesAsync(ct);
        await activity.LogAsync(new(user.UserId, r.ProjectId, isNew ? "MarketplaceAgentInstalled" : "MarketplaceAgentUpgraded", nameof(MarketplaceInstallation), installation.ID, $"{(isNew ? "Installed" : "Upgraded")} agent '{item.Title}' {version.Version}.", new Dictionary<string, object?> { ["permissions"] = approvals }), ct);
        installation.MarketplaceItem = item; installation.MarketplaceItemVersion = version;
        return ToDto(installation, user.UserId);
    }

    internal static MarketplaceInstallationItem ToDto(MarketplaceInstallation value, Guid userId) => new(value.ID, value.ProjectId,
        MarketplaceSupport.Summary(value.MarketplaceItem, userId, false, false), MarketplaceSupport.Version(value.MarketplaceItemVersion), value.Status,
        MarketplaceSupport.Strings(value.ApprovedPermissionsJson), value.InstalledAt, value.DisabledAt);
}

public sealed class SetMarketplaceInstallationStatusHandler(AppDbContext db, ICurrentUser user, IActivityLogger activity)
    : IRequestHandler<SetMarketplaceInstallationStatusCommand, MarketplaceInstallationItem>
{
    public async Task<MarketplaceInstallationItem> Handle(SetMarketplaceInstallationStatusCommand r, CancellationToken ct)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, r.ProjectId, user.UserId, ct); ProjectAccess.RequireManager(role);
        var value = await Query().SingleOrDefaultAsync(item => item.ID == r.InstallationId && item.ProjectId == r.ProjectId, ct) ?? throw new NotFoundException("Marketplace installation not found.");
        value.Status = r.Status; value.DisabledAt = r.Status == MarketplaceInstallationStatus.Disabled ? DateTime.UtcNow : null; await db.SaveChangesAsync(ct);
        await activity.LogAsync(new(user.UserId, r.ProjectId, "MarketplaceInstallationStatusChanged", nameof(MarketplaceInstallation), value.ID, $"Marketplace installation changed to {r.Status}."), ct);
        return InstallMarketplaceAgentHandler.ToDto(value, user.UserId);
    }
    private IQueryable<MarketplaceInstallation> Query() => db.MarketplaceInstallations.Include(value => value.MarketplaceItem).ThenInclude(value => value.Author).Include(value => value.MarketplaceItem).ThenInclude(value => value.Versions).Include(value => value.MarketplaceItemVersion);
}

public sealed class UninstallMarketplaceItemHandler(AppDbContext db, ICurrentUser user, IActivityLogger activity) : IRequestHandler<UninstallMarketplaceItemCommand>
{
    public async Task Handle(UninstallMarketplaceItemCommand r, CancellationToken ct)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, r.ProjectId, user.UserId, ct); ProjectAccess.RequireManager(role);
        var value = await db.MarketplaceInstallations.SingleOrDefaultAsync(item => item.ID == r.InstallationId && item.ProjectId == r.ProjectId, ct) ?? throw new NotFoundException("Marketplace installation not found.");
        value.IsDeleted = true; value.DeletedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        await activity.LogAsync(new(user.UserId, r.ProjectId, "MarketplaceItemUninstalled", nameof(MarketplaceInstallation), value.ID, "Uninstalled marketplace package."), ct);
    }
}

public sealed class ListMarketplaceInstallationsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<ListMarketplaceInstallationsQuery, IReadOnlyList<MarketplaceInstallationItem>>
{
    public async Task<IReadOnlyList<MarketplaceInstallationItem>> Handle(ListMarketplaceInstallationsQuery r, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, r.ProjectId, user.UserId, ct);
        var values = await db.MarketplaceInstallations.Include(value => value.MarketplaceItem).ThenInclude(value => value.Author).Include(value => value.MarketplaceItem).ThenInclude(value => value.Versions).Include(value => value.MarketplaceItemVersion).Where(value => value.ProjectId == r.ProjectId).OrderByDescending(value => value.InstalledAt).ToListAsync(ct);
        return values.Select(value => InstallMarketplaceAgentHandler.ToDto(value, user.UserId)).ToArray();
    }
}
