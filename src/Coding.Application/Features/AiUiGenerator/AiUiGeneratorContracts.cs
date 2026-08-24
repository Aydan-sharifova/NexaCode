using Coding.Enums;

namespace Coding.Application.Features.AiUiGenerator;

public sealed record AiUiFileDto(string Path, Guid? ExistingNodeId, string? ExistingContent, string GeneratedContent, string? ConcurrencyToken);
public sealed record AiUiGenerationDto(Guid Id, Guid ProjectId, string Prompt, bool IncludeSampleData, ScreenshotGenerationStatus Status,
    string Analysis, string PreviewHtml, IReadOnlyList<AiUiFileDto> Files, string? ModelProvider, string? ModelName, DateTime GeneratedAt, DateTime? AppliedAt);
public interface IAiUiGeneratorService
{
    Task<AiUiGenerationDto> GenerateAsync(Guid projectId, string prompt, bool includeSampleData, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiUiGenerationDto>> ListAsync(Guid projectId, int take, CancellationToken cancellationToken);
    Task<AiUiGenerationDto> GetAsync(Guid projectId, Guid id, CancellationToken cancellationToken);
    Task<AiUiGenerationDto> ApplyAsync(Guid projectId, Guid id, bool confirm, CancellationToken cancellationToken);
}
