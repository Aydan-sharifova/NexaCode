using Coding.Enums;

namespace Coding.Application.Features.ScreenshotToCode;

public sealed record ScreenshotFileDto(string Path, Guid? ExistingNodeId, string? ExistingContent, string GeneratedContent, string? ConcurrencyToken);
public sealed record ScreenshotGenerationDto(Guid Id, Guid ProjectId, string Prompt, string ImageFileName, string ImageMediaType,
    string ImageHash, ScreenshotGenerationStatus Status, string Analysis, string PreviewHtml,
    IReadOnlyList<ScreenshotFileDto> Files, string? ModelProvider, string? ModelName, string? FailureReason,
    DateTime GeneratedAt, DateTime? AppliedAt);
public sealed record CreateScreenshotGeneration(Guid ProjectId, string Prompt, string FileName, string MediaType, byte[] Image);

public interface IScreenshotToCodeService
{
    Task<ScreenshotGenerationDto> GenerateAsync(CreateScreenshotGeneration request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScreenshotGenerationDto>> ListAsync(Guid projectId, int take, CancellationToken cancellationToken);
    Task<ScreenshotGenerationDto> GetAsync(Guid projectId, Guid generationId, CancellationToken cancellationToken);
    Task<ScreenshotGenerationDto> ApplyAsync(Guid projectId, Guid generationId, bool confirm, CancellationToken cancellationToken);
}
