using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Deployments;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Deployments;

public sealed class ProjectDeploymentService(AppDbContext db, ICurrentUser current, IActivityLogger activity) : IProjectDeploymentService
{
    private const int MaximumFiles = 250;
    private const int MaximumCharacters = 2_000_000;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".html", ".htm", ".css", ".js", ".mjs", ".json", ".svg", ".txt", ".xml", ".webmanifest" };

    public async Task<IReadOnlyList<DeploymentSummary>> ListAsync(Guid projectId, string origin, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, current.UserId, ct);
        var items = await db.ProjectDeployments.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.Version).Take(50).ToListAsync(ct);
        return items.Select(x => Map(x, origin)).ToList();
    }

    public async Task<DeploymentSummary> DeployAsync(Guid projectId, string origin, CancellationToken ct)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, projectId, current.UserId, ct);
        ProjectAccess.RequireRepositoryWrite(role);
        await ProjectAccess.EnsureWorkspaceWritableAsync(db, projectId, role, ct);
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.ID == projectId, ct) ?? throw new NotFoundException("Project not found.");
        if (!project.IsPublic) throw new ForbiddenException("Only public projects can create a public deployment.");
        var nodes = await db.WorkspaceNodes.AsNoTracking().Include(x => x.FileContent).Where(x => x.ProjectId == projectId).ToListAsync(ct);
        var byId = nodes.ToDictionary(x => x.ID);
        string PathOf(WorkspaceNode node)
        {
            var parts = new Stack<string>();
            for (var cursor = node; ;)
            {
                parts.Push(cursor.Name);
                if (!cursor.ParentId.HasValue || !byId.TryGetValue(cursor.ParentId.Value, out cursor!)) break;
            }
            return string.Join('/', parts);
        }
        var files = nodes.Where(x => x.NodeType == WorkspaceNodeType.File && x.FileContent is { IsBinary: false }).Select(x => new { Path = PathOf(x), Content = x.FileContent!.Content }).Where(x => AllowedExtensions.Contains(System.IO.Path.GetExtension(x.Path))).OrderBy(x => x.Path, StringComparer.Ordinal).ToList();
        if (!files.Any(x => x.Path.Equals("index.html", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("A root index.html file is required for static deployment.");
        if (files.Count > MaximumFiles || files.Sum(x => x.Content.Length) > MaximumCharacters) throw new InvalidOperationException("Static deployment exceeds the 250 file or 2,000,000 character limit.");
        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", files.Select(x => $"{x.Path}\0{x.Content}"))))).ToLowerInvariant();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await db.ProjectDeployments.Where(x => x.ProjectId == projectId && x.IsActive).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.UpdateAt, DateTime.UtcNow), ct);
        var version = (await db.ProjectDeployments.Where(x => x.ProjectId == projectId).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var commit = await db.GitCommits.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CommitDate).Select(x => x.CommitHash).FirstOrDefaultAsync(ct);
        var slugBase = Regex.Replace(project.Name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slugBase.Length == 0) slugBase = "project";
        if (slugBase.Length > 50) slugBase = slugBase[..50].TrimEnd('-');
        var deployment = new ProjectDeployment { ID = Guid.NewGuid(), ProjectId = projectId, DeployedById = current.UserId, Slug = $"{slugBase}-{Guid.NewGuid():N}"[..Math.Min(slugBase.Length + 9, 59)], Version = version, SourceHash = sourceHash, CommitSha = commit, DeployedAt = DateTime.UtcNow, CreatAt = DateTime.UtcNow, IsActive = true };
        deployment.Files = files.Select(x => new ProjectDeploymentFile { DeploymentId = deployment.ID, Path = x.Path, Content = x.Content, ContentType = ContentType(x.Path), ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(x.Content))).ToLowerInvariant() }).ToList();
        db.ProjectDeployments.Add(deployment);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await activity.LogAsync(new(current.UserId, projectId, "DeploymentSucceeded", nameof(ProjectDeployment), deployment.ID, $"Deployed static site version {version}.", new Dictionary<string, object?> { ["version"] = version, ["sourceHash"] = sourceHash, ["commitSha"] = commit }), ct);
        return Map(deployment, origin);
    }

    public async Task<DeploymentAsset?> GetPublicAssetAsync(string slug, string? path, CancellationToken ct)
    {
        path = DeploymentPathPolicy.Normalize(path);
        if (path is null) return null;
        return await db.ProjectDeploymentFiles.AsNoTracking().Where(x => x.Deployment.Slug == slug && x.Deployment.IsActive && x.Deployment.Project.IsPublic && x.Path == path).Select(x => new DeploymentAsset(x.Content, x.ContentType)).SingleOrDefaultAsync(ct);
    }

    private static DeploymentSummary Map(ProjectDeployment x, string origin) => new(x.ID, x.ProjectId, x.Slug, x.Version, x.SourceHash, x.CommitSha, x.DeployedAt, x.IsActive, $"{origin.TrimEnd('/')}/deploy/{x.Slug}/");
    private static string ContentType(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() switch { ".html" or ".htm" => "text/html; charset=utf-8", ".css" => "text/css; charset=utf-8", ".js" or ".mjs" => "text/javascript; charset=utf-8", ".json" or ".webmanifest" => "application/json; charset=utf-8", ".svg" => "image/svg+xml", ".xml" => "application/xml; charset=utf-8", _ => "text/plain; charset=utf-8" };
}
