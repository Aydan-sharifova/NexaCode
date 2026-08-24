using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coding.Application.Abstractions;
using Coding.Application.Features.AiAssistant;
using Coding.Application.Features.KnowledgeGraph;
using Coding.Application.Features.Repositories;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.KnowledgeGraph;

public sealed class KnowledgeGraphService(AppDbContext db, ICurrentUser currentUser, IProjectRepositoryCoordinator coordinator, IAiProvider provider, ILogger<KnowledgeGraphService> logger) : IKnowledgeGraphService
{
    private const int MaxFiles = 2_000;
    private const int MaxFileCharacters = 524_288;
    private const int MaxTotalCharacters = 10_485_760;
    private const int MaxResponseNodes = 5_000;
    private const int MaxResponseEdges = 15_000;

    public async Task<KnowledgeGraphDto> IndexAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var role = await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, cancellationToken); ProjectAccess.RequireRepositoryWrite(role);
        await using var lease = await coordinator.AcquireAsync(projectId, cancellationToken);
        var rawNodes = await db.WorkspaceNodes.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.ID)
            .Select(x => new { x.ID, x.ParentId, x.Name, x.NodeType, Content = x.FileContent == null || x.FileContent.IsBinary ? null : x.FileContent.Content, Hash = x.FileContent == null ? "" : x.FileContent.ContentHash, Version = x.FileContent == null ? 0 : x.FileContent.VersionNumber }).ToListAsync(cancellationToken);
        var textNodes = rawNodes.Where(x => x.NodeType == WorkspaceNodeType.File && x.Content is not null).ToList();
        if (textNodes.Count > MaxFiles) throw new ConflictException($"Knowledge graph indexing is limited to {MaxFiles} text files per snapshot.");
        if (textNodes.Any(x => x.Content!.Length > MaxFileCharacters) || textNodes.Sum(x => (long)x.Content!.Length) > MaxTotalCharacters) throw new ConflictException("The text workspace exceeds the bounded knowledge graph indexing budget.");
        var byId = rawNodes.ToDictionary(x => x.ID); string Path(Guid id) { var parts = new List<string>(); var current = byId[id]; var seen = new HashSet<Guid>(); while (true) { if (!seen.Add(current.ID)) throw new ConflictException("The workspace tree contains a parent cycle."); parts.Add(current.Name); if (!current.ParentId.HasValue || !byId.TryGetValue(current.ParentId.Value, out var parent)) break; current = parent; } parts.Reverse(); return string.Join('/', parts); }
        var sources = textNodes.Select(x => new GraphSourceFile(x.ID, Path(x.ID), x.Content!)).ToArray();
        var fingerprintInput = string.Join('\n', textNodes.OrderBy(x => x.ID).Select(x => $"{x.ID:N}:{x.Version}:{x.Hash}:{Path(x.ID)}"));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)));
        var current = await db.KnowledgeGraphSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.IsCurrent, cancellationToken);
        if (current?.SourceFingerprint == fingerprint) return await Map(current.ID, cancellationToken);
        var extracted = KnowledgeGraphExtractor.Extract(sources); var now = DateTime.UtcNow;
        var strategy = db.Database.CreateExecutionStrategy();
        var snapshotId = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.KnowledgeGraphSnapshots.Where(x => x.ProjectId == projectId && x.IsCurrent).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsCurrent, false), cancellationToken);
            var version = (await db.KnowledgeGraphSnapshots.Where(x => x.ProjectId == projectId).MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0) + 1;
            var snapshot = new KnowledgeGraphSnapshot { ID = Guid.NewGuid(), ProjectId = projectId, Version = version, SourceFingerprint = fingerprint, IsCurrent = true, FileCount = sources.Length, NodeCount = extracted.Nodes.Count, EdgeCount = extracted.Edges.Count, IndexedAt = now, IndexedByUserId = currentUser.UserId, CreatAt = now };
            snapshot.Nodes = extracted.Nodes.Select(x => new KnowledgeGraphNode { Id = x.Id, Snapshot = snapshot, SourceFileId = x.SourceFileId, Kind = x.Kind, Key = x.Key, Name = x.Name, Path = x.Path, Line = x.Line, Metadata = JsonDocument.Parse("{}") }).ToList();
            snapshot.Edges = extracted.Edges.Select(x => new KnowledgeGraphEdge { Id = x.Id, Snapshot = snapshot, FromNodeId = x.FromNodeId, ToNodeId = x.ToNodeId, Kind = x.Kind, Confidence = x.Confidence, Evidence = x.Evidence }).ToList();
            db.KnowledgeGraphSnapshots.Add(snapshot); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return snapshot.ID;
        });
        return await Map(snapshotId, cancellationToken);
    }

    public async Task<KnowledgeGraphDto> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, cancellationToken);
        var id = await db.KnowledgeGraphSnapshots.AsNoTracking().Where(x => x.ProjectId == projectId && x.IsCurrent).Select(x => (Guid?)x.ID).SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Index this project to create its knowledge graph.");
        return await Map(id, cancellationToken);
    }

    public async Task<ImpactAnalysisDto> ImpactAsync(Guid projectId, Guid nodeId, bool generateReport, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, cancellationToken);
        var snapshot = await db.KnowledgeGraphSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.IsCurrent, cancellationToken) ?? throw new NotFoundException("Index this project to analyze impact.");
        var nodes = await db.KnowledgeGraphNodes.AsNoTracking().Where(x => x.SnapshotId == snapshot.ID).Select(x => new KnowledgeGraphNodeDto(x.Id, x.SourceFileId, x.Kind, x.Name, x.Path, x.Line)).ToListAsync(cancellationToken);
        var selected = nodes.SingleOrDefault(x => x.Id == nodeId) ?? throw new NotFoundException("Knowledge graph node not found.");
        var edges = await db.KnowledgeGraphEdges.AsNoTracking().Where(x => x.SnapshotId == snapshot.ID).Select(x => new { x.FromNodeId, x.ToNodeId, x.Kind, x.Confidence }).ToListAsync(cancellationToken);
        var byId = nodes.ToDictionary(x => x.Id); var incoming = Traverse(nodeId, false); var outgoing = Traverse(nodeId, true); var neighborhood = TraverseAny(nodeId);
        IReadOnlyList<ImpactItem> Category(IEnumerable<(Guid Id, int Distance, KnowledgeEdgeKind Kind, decimal Confidence)> values, params KnowledgeNodeKind[] kinds) => values.Where(x => byId.TryGetValue(x.Id, out var node) && kinds.Contains(node.Kind)).GroupBy(x => x.Id).Select(x => x.OrderBy(v => v.Distance).First()).Take(200).Select(x => Item(byId[x.Id], x)).ToArray();
        var usedBy = incoming.Take(200).Select(x => Item(byId[x.Id], x)).ToArray(); var dependencies = outgoing.Take(200).Select(x => Item(byId[x.Id], x)).ToArray();
        var affectedFiles = neighborhood.Where(x => byId[x.Id].Kind == KnowledgeNodeKind.File).GroupBy(x => x.Id).Select(x => x.OrderBy(v => v.Distance).First()).Take(200).Select(x => Item(byId[x.Id], x)).ToArray();
        var result = new ImpactAnalysisDto(snapshot.ID, selected.Id, selected.Name, affectedFiles, Category(neighborhood, KnowledgeNodeKind.Service, KnowledgeNodeKind.Controller), Category(neighborhood, KnowledgeNodeKind.ApiEndpoint), Category(neighborhood, KnowledgeNodeKind.Test), Category(neighborhood, KnowledgeNodeKind.DatabaseTable), Category(neighborhood, KnowledgeNodeKind.Component), dependencies, usedBy, null, false, provider.ProviderName, provider.Model);
        if (!generateReport) return result;
        try
        {
            var prompt = BuildImpactPrompt(result); var output = new StringBuilder();
            var request = new AiRequest("You are a code impact analyst. Use only the supplied knowledge-graph evidence. Clearly distinguish direct and transitive relationships, include confidence caveats, and never claim a breakage or dependency not present in the evidence. Do not request or invent source code.", prompt, string.Empty, "general", AiAssistantAction.Explain, [], MaxOutputTokens: 1_200);
            await foreach (var chunk in provider.StreamAsync(request, cancellationToken).WithCancellation(cancellationToken)) if (!chunk.IsCompleted && output.Length < 12_000) output.Append(chunk.Content.AsSpan(0, Math.Min(chunk.Content.Length, 12_000 - output.Length)));
            var report = output.ToString().Trim(); return result with { ModelReport = report.Length == 0 ? null : report, ModelAvailable = report.Length > 0 };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogWarning(exception, "Ollama impact report unavailable for project {ProjectId}, node {NodeId}.", projectId, nodeId); return result; }

        List<(Guid Id, int Distance, KnowledgeEdgeKind Kind, decimal Confidence)> Traverse(Guid start, bool forward)
        {
            var result = new List<(Guid, int, KnowledgeEdgeKind, decimal)>(); var seen = new HashSet<Guid> { start }; var queue = new Queue<(Guid Id, int Distance)>(); queue.Enqueue((start, 0));
            while (queue.Count > 0 && result.Count < 500) { var current = queue.Dequeue(); if (current.Distance >= 3) continue; var next = forward ? edges.Where(x => x.FromNodeId == current.Id).Select(x => (x.ToNodeId, x.Kind, x.Confidence)) : edges.Where(x => x.ToNodeId == current.Id).Select(x => (x.FromNodeId, x.Kind, x.Confidence)); foreach (var item in next) if (seen.Add(item.Item1)) { result.Add((item.Item1, current.Distance + 1, item.Kind, item.Confidence)); queue.Enqueue((item.Item1, current.Distance + 1)); } }
            return result;
        }
        List<(Guid Id, int Distance, KnowledgeEdgeKind Kind, decimal Confidence)> TraverseAny(Guid start)
        {
            var result = new List<(Guid, int, KnowledgeEdgeKind, decimal)>(); var seen = new HashSet<Guid> { start }; var queue = new Queue<(Guid Id, int Distance)>(); queue.Enqueue((start, 0));
            while (queue.Count > 0 && result.Count < 700) { var current = queue.Dequeue(); if (current.Distance >= 3) continue; var next = edges.Where(x => x.FromNodeId == current.Id || x.ToNodeId == current.Id).Select(x => (Id: x.FromNodeId == current.Id ? x.ToNodeId : x.FromNodeId, x.Kind, x.Confidence)); foreach (var item in next) if (seen.Add(item.Id)) { result.Add((item.Id, current.Distance + 1, item.Kind, item.Confidence)); queue.Enqueue((item.Id, current.Distance + 1)); } }
            return result;
        }
    }

    private async Task<KnowledgeGraphDto> Map(Guid snapshotId, CancellationToken cancellationToken)
    {
        var snapshot = await db.KnowledgeGraphSnapshots.AsNoTracking().SingleAsync(x => x.ID == snapshotId, cancellationToken);
        var nodes = await db.KnowledgeGraphNodes.AsNoTracking().Where(x => x.SnapshotId == snapshotId).OrderBy(x => x.Kind).ThenBy(x => x.Key).Take(MaxResponseNodes).Select(x => new KnowledgeGraphNodeDto(x.Id, x.SourceFileId, x.Kind, x.Name, x.Path, x.Line)).ToListAsync(cancellationToken);
        var ids = nodes.Select(x => x.Id).ToArray(); var edges = await db.KnowledgeGraphEdges.AsNoTracking().Where(x => x.SnapshotId == snapshotId && ids.Contains(x.FromNodeId) && ids.Contains(x.ToNodeId)).OrderBy(x => x.Kind).ThenBy(x => x.Id).Take(MaxResponseEdges).Select(x => new KnowledgeGraphEdgeDto(x.Id, x.FromNodeId, x.ToNodeId, x.Kind, x.Confidence, x.Evidence)).ToListAsync(cancellationToken);
        return new(snapshot.ID, snapshot.ProjectId, snapshot.Version, snapshot.SourceFingerprint, snapshot.IndexedAt, snapshot.FileCount, snapshot.NodeCount, snapshot.EdgeCount, snapshot.NodeCount > nodes.Count || snapshot.EdgeCount > edges.Count, nodes, edges);
    }
    private static ImpactItem Item(KnowledgeGraphNodeDto node, (Guid Id, int Distance, KnowledgeEdgeKind Kind, decimal Confidence) relation) => new(node.Id, node.SourceFileId, node.Kind, node.Name, node.Path, relation.Distance, relation.Kind.ToString(), relation.Confidence);
    private static string BuildImpactPrompt(ImpactAnalysisDto result)
    {
        static string Names(IEnumerable<ImpactItem> items) => string.Join(", ", items.Take(40).Select(x => $"{x.Kind}:{x.Name} (distance {x.Distance}, {x.Relationship}, confidence {x.Confidence:0.00})"));
        return $"Selected node: {result.SelectedName}. Affected files: {Names(result.AffectedFiles)}. Services/controllers: {Names(result.Services)}. APIs: {Names(result.ApiEndpoints)}. Tests: {Names(result.Tests)}. Database objects: {Names(result.DatabaseObjects)}. Frontend components: {Names(result.FrontendComponents)}. Direct/transitive dependencies: {Names(result.Dependencies)}. Used by: {Names(result.UsedBy)}. Produce a concise impact report with Potential Impact, Highest Risk, Verification Steps, and confidence limitations.";
    }
}

public sealed class IndexKnowledgeGraphHandler(IKnowledgeGraphService service) : MediatR.IRequestHandler<IndexKnowledgeGraphCommand, KnowledgeGraphDto> { public Task<KnowledgeGraphDto> Handle(IndexKnowledgeGraphCommand request, CancellationToken ct) => service.IndexAsync(request.ProjectId, ct); }
public sealed class GetKnowledgeGraphHandler(IKnowledgeGraphService service) : MediatR.IRequestHandler<GetKnowledgeGraphQuery, KnowledgeGraphDto> { public Task<KnowledgeGraphDto> Handle(GetKnowledgeGraphQuery request, CancellationToken ct) => service.GetAsync(request.ProjectId, ct); }
public sealed class GetImpactAnalysisHandler(IKnowledgeGraphService service) : MediatR.IRequestHandler<GetImpactAnalysisQuery, ImpactAnalysisDto> { public Task<ImpactAnalysisDto> Handle(GetImpactAnalysisQuery request, CancellationToken ct) => service.ImpactAsync(request.ProjectId, request.NodeId, false, ct); }
public sealed class GenerateImpactReportHandler(IKnowledgeGraphService service) : MediatR.IRequestHandler<GenerateImpactReportCommand, ImpactAnalysisDto> { public Task<ImpactAnalysisDto> Handle(GenerateImpactReportCommand request, CancellationToken ct) => service.ImpactAsync(request.ProjectId, request.NodeId, true, ct); }
