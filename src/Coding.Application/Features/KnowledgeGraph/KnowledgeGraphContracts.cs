using Coding.Enums;
using MediatR;

namespace Coding.Application.Features.KnowledgeGraph;

public sealed record KnowledgeGraphNodeDto(Guid Id, Guid? SourceFileId, KnowledgeNodeKind Kind, string Name, string? Path, int? Line);
public sealed record KnowledgeGraphEdgeDto(Guid Id, Guid FromNodeId, Guid ToNodeId, KnowledgeEdgeKind Kind, decimal Confidence, string Evidence);
public sealed record KnowledgeGraphDto(Guid SnapshotId, Guid ProjectId, int Version, string SourceFingerprint, DateTime IndexedAt, int FileCount, int TotalNodes, int TotalEdges, bool IsTruncated, IReadOnlyList<KnowledgeGraphNodeDto> Nodes, IReadOnlyList<KnowledgeGraphEdgeDto> Edges);
public sealed record ImpactItem(Guid NodeId, Guid? SourceFileId, KnowledgeNodeKind Kind, string Name, string? Path, int Distance, string Relationship, decimal Confidence);
public sealed record ImpactAnalysisDto(Guid SnapshotId, Guid SelectedNodeId, string SelectedName, IReadOnlyList<ImpactItem> AffectedFiles, IReadOnlyList<ImpactItem> Services, IReadOnlyList<ImpactItem> ApiEndpoints, IReadOnlyList<ImpactItem> Tests, IReadOnlyList<ImpactItem> DatabaseObjects, IReadOnlyList<ImpactItem> FrontendComponents, IReadOnlyList<ImpactItem> Dependencies, IReadOnlyList<ImpactItem> UsedBy, string? ModelReport, bool ModelAvailable, string Provider, string? Model);

public sealed record IndexKnowledgeGraphCommand(Guid ProjectId) : IRequest<KnowledgeGraphDto>;
public sealed record GetKnowledgeGraphQuery(Guid ProjectId) : IRequest<KnowledgeGraphDto>;
public sealed record GetImpactAnalysisQuery(Guid ProjectId, Guid NodeId) : IRequest<ImpactAnalysisDto>;
public sealed record GenerateImpactReportCommand(Guid ProjectId, Guid NodeId) : IRequest<ImpactAnalysisDto>;

public interface IKnowledgeGraphService
{
    Task<KnowledgeGraphDto> IndexAsync(Guid projectId, CancellationToken cancellationToken);
    Task<KnowledgeGraphDto> GetAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ImpactAnalysisDto> ImpactAsync(Guid projectId, Guid nodeId, bool generateReport, CancellationToken cancellationToken);
}
