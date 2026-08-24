using System.Text.Json;
using Coding.Enums;

namespace Coding.Models;

public sealed class KnowledgeGraphSnapshot : Base
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int Version { get; set; }
    public string SourceFingerprint { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public int FileCount { get; set; }
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public DateTime IndexedAt { get; set; }
    public Guid IndexedByUserId { get; set; }
    public ICollection<KnowledgeGraphNode> Nodes { get; set; } = [];
    public ICollection<KnowledgeGraphEdge> Edges { get; set; } = [];
}

public sealed class KnowledgeGraphNode
{
    public Guid Id { get; set; }
    public Guid SnapshotId { get; set; }
    public KnowledgeGraphSnapshot Snapshot { get; set; } = null!;
    public Guid? SourceFileId { get; set; }
    public WorkspaceNode? SourceFile { get; set; }
    public KnowledgeNodeKind Kind { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public int? Line { get; set; }
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public ICollection<KnowledgeGraphEdge> Outgoing { get; set; } = [];
    public ICollection<KnowledgeGraphEdge> Incoming { get; set; } = [];
}

public sealed class KnowledgeGraphEdge
{
    public Guid Id { get; set; }
    public Guid SnapshotId { get; set; }
    public KnowledgeGraphSnapshot Snapshot { get; set; } = null!;
    public Guid FromNodeId { get; set; }
    public KnowledgeGraphNode FromNode { get; set; } = null!;
    public Guid ToNodeId { get; set; }
    public KnowledgeGraphNode ToNode { get; set; } = null!;
    public KnowledgeEdgeKind Kind { get; set; }
    public decimal Confidence { get; set; }
    public string Evidence { get; set; } = string.Empty;
}
