using System.Text.Json;
using Coding.Enums;
using FluentValidation;
using MediatR;

namespace Coding.Application.Features.Marketplace;

public sealed record MarketplaceAuthor(Guid Id, string PublicId, string UserName, string FullName, string? AvatarUrl);
public sealed record MarketplaceVersionItem(Guid Id, string Version, IReadOnlyList<string> Permissions, string? Changelog, string Checksum, bool IsPublished, DateTime CreatedAt, DateTime? PublishedAt);
public sealed record MarketplaceItemSummary(Guid Id, string Slug, string Title, string Description, MarketplaceCategory Category, MarketplaceItemStatus Status,
    MarketplaceAuthor Author, IReadOnlyList<string> Tags, int Downloads, int Likes, bool IsLiked, bool IsSaved, MarketplaceVersionItem? LatestVersion, DateTime UpdatedAt);
public sealed record MarketplaceItemDetails(MarketplaceItemSummary Item, IReadOnlyList<MarketplaceVersionItem> Versions, JsonElement? Manifest, bool CanManage);
public sealed record MarketplaceInstallationItem(Guid Id, Guid ProjectId, MarketplaceItemSummary Item, MarketplaceVersionItem Version,
    MarketplaceInstallationStatus Status, IReadOnlyList<string> ApprovedPermissions, DateTime InstalledAt, DateTime? DisabledAt);

public static class MarketplacePermissions
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
        { "editor.read", "editor.write", "project.read", "project.write", "network.request", "terminal.run" };
    public static readonly IReadOnlySet<string> Dangerous = new HashSet<string>(StringComparer.Ordinal)
        { "editor.write", "project.write", "network.request", "terminal.run" };
}

public sealed record CreateMarketplaceItemCommand(string Title, string Description, MarketplaceCategory Category, IReadOnlyList<string> Tags,
    string Version, JsonElement Manifest, IReadOnlyList<string> Permissions, string? Changelog) : IRequest<MarketplaceItemDetails>;
public sealed record AddMarketplaceVersionCommand(Guid ItemId, string Version, JsonElement Manifest, IReadOnlyList<string> Permissions, string? Changelog) : IRequest<MarketplaceItemDetails>;
public sealed record PublishMarketplaceVersionCommand(Guid ItemId, Guid VersionId) : IRequest<MarketplaceItemDetails>;
public sealed record GetMarketplaceItemQuery(string Slug) : IRequest<MarketplaceItemDetails>;
public sealed record ListMarketplaceItemsQuery(MarketplaceCategory? Category, string? Search, bool Mine = false, bool Saved = false, int Skip = 0, int Take = 30) : IRequest<IReadOnlyList<MarketplaceItemSummary>>;
public sealed record SetMarketplaceLikeCommand(Guid ItemId, bool Liked) : IRequest<MarketplaceItemSummary>;
public sealed record SetMarketplaceSavedCommand(Guid ItemId, bool Saved) : IRequest<MarketplaceItemSummary>;
public sealed record InstallMarketplaceAgentCommand(Guid ProjectId, Guid ItemId, Guid VersionId, IReadOnlyList<string> ApprovedDangerousPermissions) : IRequest<MarketplaceInstallationItem>;
public sealed record SetMarketplaceInstallationStatusCommand(Guid ProjectId, Guid InstallationId, MarketplaceInstallationStatus Status) : IRequest<MarketplaceInstallationItem>;
public sealed record UninstallMarketplaceItemCommand(Guid ProjectId, Guid InstallationId) : IRequest;
public sealed record ListMarketplaceInstallationsQuery(Guid ProjectId) : IRequest<IReadOnlyList<MarketplaceInstallationItem>>;

public interface IMarketplaceManifestValidator
{
    MarketplaceValidatedManifest Validate(MarketplaceCategory category, JsonElement manifest, IReadOnlyList<string> permissions);
}
public sealed record MarketplaceValidatedManifest(string ManifestJson, string PermissionsJson, IReadOnlyList<string> Permissions, string Checksum);

internal static class MarketplaceValidationRules
{
    public const string SemanticVersionPattern = "^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-[0-9A-Za-z.-]+)?$";
}

public sealed class CreateMarketplaceItemValidator : AbstractValidator<CreateMarketplaceItemCommand>
{
    public CreateMarketplaceItemValidator()
    {
        RuleFor(item => item.Title).NotEmpty().MaximumLength(160);
        RuleFor(item => item.Description).NotEmpty().MaximumLength(4000);
        RuleFor(item => item.Tags).NotNull().Must(tags => tags.Count <= 12 && tags.All(tag => !string.IsNullOrWhiteSpace(tag) && tag.Trim().Length <= 40));
        RuleFor(item => item.Version).Matches(MarketplaceValidationRules.SemanticVersionPattern);
        RuleFor(item => item.Changelog).MaximumLength(4000);
    }
}
public sealed class AddMarketplaceVersionValidator : AbstractValidator<AddMarketplaceVersionCommand>
{
    public AddMarketplaceVersionValidator()
    {
        RuleFor(item => item.ItemId).NotEmpty();
        RuleFor(item => item.Version).Matches(MarketplaceValidationRules.SemanticVersionPattern);
        RuleFor(item => item.Changelog).MaximumLength(4000);
    }
}
