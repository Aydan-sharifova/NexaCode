using Coding.Enums;

namespace Coding.Models;

public sealed class MarketplaceItem : Base
{
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MarketplaceCategory Category { get; set; }
    public MarketplaceItemStatus Status { get; set; } = MarketplaceItemStatus.Draft;
    public string TagsJson { get; set; } = "[]";
    public int DownloadCount { get; set; }
    public int LikeCount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public ICollection<MarketplaceItemVersion> Versions { get; set; } = [];
    public ICollection<MarketplaceInstallation> Installations { get; set; } = [];
}

public sealed class MarketplaceItemVersion : Base
{
    public Guid MarketplaceItemId { get; set; }
    public MarketplaceItem MarketplaceItem { get; set; } = null!;
    public string Version { get; set; } = string.Empty;
    public string ManifestJson { get; set; } = "{}";
    public string PermissionsJson { get; set; } = "[]";
    public string Checksum { get; set; } = string.Empty;
    public string? Changelog { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public sealed class MarketplaceInstallation : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid MarketplaceItemId { get; set; }
    public MarketplaceItem MarketplaceItem { get; set; } = null!;
    public Guid MarketplaceItemVersionId { get; set; }
    public MarketplaceItemVersion MarketplaceItemVersion { get; set; } = null!;
    public Guid InstalledById { get; set; }
    public User InstalledBy { get; set; } = null!;
    public MarketplaceInstallationStatus Status { get; set; } = MarketplaceInstallationStatus.Active;
    public string ApprovedPermissionsJson { get; set; } = "[]";
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    public DateTime? DisabledAt { get; set; }
}

public sealed class MarketplaceLike
{
    public Guid MarketplaceItemId { get; set; }
    public MarketplaceItem MarketplaceItem { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class SavedMarketplaceItem
{
    public Guid MarketplaceItemId { get; set; }
    public MarketplaceItem MarketplaceItem { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
