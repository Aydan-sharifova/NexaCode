namespace Coding.Application.Features.Deployments;

public sealed record DeploymentSummary(Guid Id, Guid ProjectId, string Slug, int Version, string SourceHash, string? CommitSha, DateTime DeployedAt, bool IsActive, string Url);
public sealed record DeploymentAsset(string Content, string ContentType);

public static class DeploymentPathPolicy
{
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "index.html";
        try { path = Uri.UnescapeDataString(path).Replace('\\', '/').TrimStart('/'); }
        catch (UriFormatException) { return null; }
        if (path.Length is 0 or > 500 || path.Contains('\0') || path.Split('/').Any(x => x is "" or "." or "..")) return null;
        return path;
    }
}

public interface IProjectDeploymentService
{
    Task<IReadOnlyList<DeploymentSummary>> ListAsync(Guid projectId, string origin, CancellationToken ct);
    Task<DeploymentSummary> DeployAsync(Guid projectId, string origin, CancellationToken ct);
    Task<DeploymentAsset?> GetPublicAssetAsync(string slug, string? path, CancellationToken ct);
}
