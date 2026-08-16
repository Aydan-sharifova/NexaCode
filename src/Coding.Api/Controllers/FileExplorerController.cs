using Coding.Application.Features.FileExplorer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Coding.Application.Features.Repositories;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api")]
public sealed class FileExplorerController(ISender sender, IGitRepositoryService git, IConfiguration configuration) : ControllerBase
{
    [HttpGet("projects/{projectId:guid}/nodes")]
    public Task<IReadOnlyList<WorkspaceNodeDto>> Tree(Guid projectId, CancellationToken ct) => sender.Send(new GetProjectFileTreeQuery(projectId), ct);
    [HttpGet("projects/{projectId:guid}/nodes/children")]
    public Task<IReadOnlyList<WorkspaceNodeDto>> Children(Guid projectId, [FromQuery] Guid? parentId, CancellationToken ct) => sender.Send(new GetFolderChildrenQuery(projectId, parentId), ct);
    [HttpPost("projects/{projectId:guid}/folders")]
    public async Task<ActionResult<WorkspaceNodeDto>> Folder(Guid projectId, CreateNodeRequest request, CancellationToken ct) => StatusCode(201, await sender.Send(new CreateFolderCommand(projectId, request.ParentId, request.Name), ct));
    [HttpPost("projects/{projectId:guid}/files")]
    public async Task<ActionResult<WorkspaceNodeDto>> File(Guid projectId, CreateFileRequest request, CancellationToken ct) => StatusCode(201, await sender.Send(new CreateFileCommand(projectId, request.ParentId, request.Name, request.Content ?? ""), ct));
    [HttpPut("nodes/{nodeId:guid}/name")]
    public Task<WorkspaceNodeDto> Rename(Guid nodeId, RenameNodeRequest request, CancellationToken ct) => sender.Send(new RenameNodeCommand(nodeId, request.Name), ct);
    [HttpPut("nodes/{nodeId:guid}/parent")]
    public Task<WorkspaceNodeDto> Move(Guid nodeId, MoveNodeRequest request, CancellationToken ct) => sender.Send(new MoveNodeCommand(nodeId, request.ParentId), ct);
    [HttpDelete("nodes/{nodeId:guid}")]
    public async Task<IActionResult> Delete(Guid nodeId, CancellationToken ct) { await sender.Send(new DeleteNodeCommand(nodeId), ct); return NoContent(); }
    [HttpPost("nodes/{nodeId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid nodeId, CancellationToken ct) { await sender.Send(new RestoreDeletedNodeCommand(nodeId), ct); return NoContent(); }
    [HttpGet("files/{nodeId:guid}/content")]
    public Task<FileContentDto> Content(Guid nodeId, CancellationToken ct) => sender.Send(new GetFileContentQuery(nodeId), ct);
    [HttpPut("files/{nodeId:guid}/content")]
    public Task<FileContentDto> Save(Guid nodeId, SaveContentRequest request, CancellationToken ct) => sender.Send(new SaveFileContentCommand(nodeId, request.Content, request.ConcurrencyToken), ct);
    [HttpGet("files/{nodeId:guid}/versions")]
    public Task<IReadOnlyList<FileVersionDto>> Versions(Guid nodeId, CancellationToken ct) => sender.Send(new GetFileVersionsQuery(nodeId), ct);
    [HttpGet("files/{nodeId:guid}/versions/{versionId:guid}")]
    public Task<FileVersionDetails> Version(Guid nodeId, Guid versionId, CancellationToken ct) => sender.Send(new GetFileVersionByIdQuery(nodeId, versionId), ct);
    [HttpGet("files/{nodeId:guid}/versions/compare")]
    public Task<VersionComparison> Compare(Guid nodeId, [FromQuery] Guid leftId, [FromQuery] Guid rightId, CancellationToken ct) => sender.Send(new CompareFileVersionsQuery(nodeId, leftId, rightId), ct);
    [HttpPost("files/{nodeId:guid}/versions/{versionId:guid}/restore")]
    public Task<FileContentDto> RestoreVersion(Guid nodeId, Guid versionId, CancellationToken ct) => sender.Send(new RestoreFileVersionCommand(nodeId, versionId), ct);

    [HttpPost("projects/{projectId:guid}/files/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<IReadOnlyList<WorkspaceNodeDto>>> Upload(
        Guid projectId, [FromForm] List<IFormFile> files, [FromForm] Guid? parentId, CancellationToken ct)
    {
        if (files.Count is < 1 or > 20) return BadRequest("Select between 1 and 20 files.");
        var maximumBytes = configuration.GetValue("WorkspaceUploads:MaxFileBytes", 10 * 1024 * 1024);
        var blocked = configuration.GetSection("WorkspaceUploads:BlockedExtensions").Get<string[]>() ?? [];
        var uploaded = new List<WorkspaceNodeDto>(files.Count);
        foreach (var file in files)
        {
            var name = Path.GetFileName(file.FileName);
            if (!string.Equals(name, file.FileName, StringComparison.Ordinal) || file.Length <= 0 || file.Length > maximumBytes)
                return BadRequest($"File '{name}' has an invalid name or size.");
            if (blocked.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
                return BadRequest($"File type '{Path.GetExtension(name)}' is not allowed.");

            await using var input = file.OpenReadStream();
            using var memory = new MemoryStream();
            await input.CopyToAsync(memory, ct);
            var bytes = memory.ToArray();
            if (IsImageExtension(name) && !IsValidImage(name, file.ContentType, bytes))
                return BadRequest($"Image '{name}' does not match a supported image format.");
            var text = IsTextFile(name) ? System.Text.Encoding.UTF8.GetString(bytes) : string.Empty;
            var node = await sender.Send(new CreateFileCommand(projectId, parentId, name, text), ct);
            await git.InitializeAsync(projectId, "main", ct);
            await git.WriteFileAsync(projectId, node.Path, bytes, ct);
            uploaded.Add(node);
        }
        return StatusCode(StatusCodes.Status201Created, uploaded);
    }

    [HttpGet("files/{nodeId:guid}/raw")]
    public async Task<IActionResult> Raw(Guid nodeId, CancellationToken ct)
    {
        var file = await sender.Send(new GetFileContentQuery(nodeId), ct);
        var projectId = await ProjectId(nodeId, ct);
        var bytes = await git.ReadFileAsync(projectId, file.Path, ct);
        return File(bytes, MimeType(file.Path), Path.GetFileName(file.Path));
    }

    private async Task<Guid> ProjectId(Guid nodeId, CancellationToken ct)
    {
        var treeNode = await sender.Send(new GetNodeDetailsQuery(nodeId), ct);
        return treeNode.ProjectId;
    }

    private static bool IsTextFile(string name) => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md", ".json", ".js", ".jsx", ".ts", ".tsx", ".css", ".scss", ".html", ".cs", ".csproj", ".sln", ".py", ".java", ".xml", ".yaml", ".yml", ".csv", ".sql" }.Contains(Path.GetExtension(name));
    private static bool IsImageExtension(string name) => new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);
    private static bool IsValidImage(string name, string contentType, byte[] bytes)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return false;
        return extension switch
        {
            ".png" => bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".jpg" or ".jpeg" => bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[^2] == 0xFF && bytes[^1] == 0xD9,
            ".gif" => bytes.AsSpan().StartsWith("GIF87a"u8) || bytes.AsSpan().StartsWith("GIF89a"u8),
            ".webp" => bytes.Length > 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }
    private static string MimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".gif" => "image/gif", ".svg" => "image/svg+xml", ".pdf" => "application/pdf", _ => "application/octet-stream" };
}

public sealed record CreateNodeRequest(Guid? ParentId, string Name);
public sealed record CreateFileRequest(Guid? ParentId, string Name, string? Content);
public sealed record RenameNodeRequest(string Name);
public sealed record MoveNodeRequest(Guid? ParentId);
public sealed record SaveContentRequest(string Content, string ConcurrencyToken);
