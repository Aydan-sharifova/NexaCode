using System.Security.Cryptography;
using System.Text;
using Coding.Application.Abstractions;
using Coding.Application.Features.Debugging;
using Coding.Application.Features.AiAssistant;
using Coding.Application.Features.AiAgent;
using Coding.Application.Features.Repositories;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Coding.Infrastructure.Debugging;

public sealed class DebuggingTimelineService(AppDbContext db, ICurrentUser currentUser, IGitRepositoryService git, IAiProvider provider, ILogger<DebuggingTimelineService> logger, IAiSecretRedactionService redaction) : IDebuggingTimelineService
{
    public async Task<Guid?> CaptureFailureAsync(DebugExecutionCapture capture, CancellationToken ct)
    {
        var now = DateTime.UtcNow; var succeeded = capture.ExitCode is 0 && !capture.TimedOut && string.IsNullOrWhiteSpace(capture.Stderr);
        var observation = new DebuggingExecutionObservation { ID=Guid.NewGuid(), ProjectId=capture.ProjectId, UserId=capture.UserId, WorkspaceNodeId=capture.WorkspaceNodeId, Kind=capture.Kind, Language=Limit(capture.Language,50), SourceHash=Hash(capture.Source), Succeeded=succeeded, ExitCode=capture.ExitCode, TimedOut=capture.TimedOut, DurationMs=Math.Max(0,capture.DurationMs), ExecutedAt=now, CreatAt=now };
        db.DebuggingExecutionObservations.Add(observation);
        if (succeeded) { await db.SaveChangesAsync(ct); await PruneObservations(capture.ProjectId, ct); return null; }
        var stderr = Limit(capture.Stderr, 16_000); var stdout = Limit(capture.Stdout, 16_000);
        var summary = FirstMeaningful(stderr) ?? (capture.TimedOut ? "Execution timed out." : $"Execution failed with exit code {capture.ExitCode?.ToString() ?? "unknown"}.");
        var stack = string.Join('\n', stderr.Split('\n').Where(x => x.Contains(" at ", StringComparison.Ordinal) || x.TrimStart().StartsWith("at ", StringComparison.Ordinal)).Take(80));
        var incident = new DebuggingIncident
        {
            ID = Guid.NewGuid(), ProjectId = capture.ProjectId, CreatedById = capture.UserId, WorkspaceNodeId = capture.WorkspaceNodeId,
            Kind = capture.Kind, Status = DebuggingIncidentStatus.Open, Language = Limit(capture.Language, 50), ErrorSummary = Limit(summary, 2_000),
            StackTrace = string.IsNullOrWhiteSpace(stack) ? null : Limit(stack, 16_000), Stdout = stdout, Stderr = stderr,
            ExitCode = capture.ExitCode, TimedOut = capture.TimedOut, DurationMs = Math.Max(0, capture.DurationMs),
            SourceHash = observation.SourceHash, OccurredAt = now, CreatAt = now, ExecutionObservation=observation
        };
        AddEvidence(incident, DebugEvidenceKind.Error, DebugEvidenceConfidence.High, "Execution error", incident.ErrorSummary, null, null, null, incident.OccurredAt);
        if (incident.StackTrace is not null) AddEvidence(incident, DebugEvidenceKind.StackTrace, DebugEvidenceConfidence.High, "Stack trace", incident.StackTrace, null, null, null, incident.OccurredAt);
        db.DebuggingIncidents.Add(incident); await db.SaveChangesAsync(ct); await PruneObservations(capture.ProjectId, ct); return incident.ID;
    }

    public async Task<DebuggingTimelineDto> ListAsync(Guid projectId, int take, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, ct); take = Math.Clamp(take, 1, 100);
        var total = await db.DebuggingIncidents.CountAsync(x => x.ProjectId == projectId, ct);
        var ids = await db.DebuggingIncidents.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.OccurredAt).Take(take).Select(x => x.ID).ToListAsync(ct);
        var items = new List<DebuggingIncidentDto>(ids.Count); foreach (var id in ids) items.Add(await Map(projectId, id, ct)); return new(items, total);
    }
    public async Task<DebuggingIncidentDto> GetAsync(Guid projectId, Guid incidentId, CancellationToken ct) { await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, ct); return await Map(projectId, incidentId, ct); }

    public async Task<DebuggingIncidentDto> AnalyzeAsync(Guid projectId, Guid incidentId, bool useModel, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, ct);
        var incident = await db.DebuggingIncidents.Include(x => x.Evidence).SingleOrDefaultAsync(x => x.ID == incidentId && x.ProjectId == projectId, ct) ?? throw new NotFoundException("Debugging incident not found.");
        if (incident.WorkspaceNodeId.HasValue)
        {
            var versions = await db.FileVersions.AsNoTracking().Where(x => x.NodeId == incident.WorkspaceNodeId && x.CreatAt <= incident.OccurredAt).OrderByDescending(x => x.CreatAt).Take(10).ToListAsync(ct);
            foreach (var version in versions) AddEvidence(incident, DebugEvidenceKind.RecentChange, version.ContentHash == incident.SourceHash ? DebugEvidenceConfidence.High : DebugEvidenceConfidence.Medium, $"File version {version.VersionNumber}", $"Saved {Relative(version.CreatAt, incident.OccurredAt)} before the failure; content hash {(version.ContentHash == incident.SourceHash ? "matches" : "differs from")} executed source.", incident.WorkspaceNodeId, version.ID, null, version.CreatAt);
            var path = await WorkspacePath(projectId, incident.WorkspaceNodeId.Value, ct);
            var baseline = await db.DebuggingExecutionObservations.AsNoTracking().Where(x => x.ProjectId == projectId && x.WorkspaceNodeId == incident.WorkspaceNodeId && x.Succeeded && x.ExecutedAt < incident.OccurredAt).OrderByDescending(x => x.ExecutedAt).FirstOrDefaultAsync(ct);
            try
            {
                using var gitTimeout=CancellationTokenSource.CreateLinkedTokenSource(ct); gitTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                var commits = (await git.GetHistoryAsync(projectId, 30, gitTimeout.Token)).Where(x => x.CommittedAt.UtcDateTime < incident.OccurredAt && x.CommittedAt.UtcDateTime >= (baseline?.ExecutedAt ?? incident.OccurredAt.AddDays(-30))).OrderByDescending(x => x.CommittedAt).ToArray();
                foreach (var commit in commits)
                {
                    var patch = (await git.GetCommitDiffAsync(projectId, commit.Sha, gitTimeout.Token)).Patch;
                    var touches=DebuggingCorrelationPolicy.CommitTouchesPath(patch,path); if (!touches) continue;
                    var supportsRegression=DebuggingCorrelationPolicy.SupportsRegressionClaim(baseline?.ExecutedAt,commit.CommittedAt.UtcDateTime,incident.OccurredAt,touches);
                    var confidence = supportsRegression ? DebugEvidenceConfidence.High : DebugEvidenceConfidence.Low;
                    AddEvidence(incident, DebugEvidenceKind.GitCommit, confidence, $"Commit {commit.ShortSha}", $"Commit '{commit.Message}' changed '{path}' {Relative(commit.CommittedAt.UtcDateTime, incident.OccurredAt)} before the failure.{(baseline is null ? " No earlier successful run exists, so causation is unproven." : $" The same file ran successfully before this commit at {baseline.ExecutedAt:O}.")}", incident.WorkspaceNodeId, null, commit.Sha, commit.CommittedAt.UtcDateTime);
                    if (supportsRegression && incident.RelevantCommitSha is null) { incident.RelevantCommitSha=commit.Sha; incident.RegressionConfidence=DebugEvidenceConfidence.High; incident.LikelyRegression=$"The failure appeared after commit {commit.ShortSha}, which changed '{path}' after the last successful run. This is a strong temporal correlation, not proof of causation."; }
                }
            }
            catch (InvalidOperationException) { /* A project without an initialized/non-empty repository has no Git evidence. */ }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { logger.LogWarning("Git evidence timed out for debugging incident {IncidentId}.",incident.ID); }
        }
        var logs = await db.ActivityLogs.AsNoTracking().Where(x => x.ProjectId == projectId && x.CreatedAt <= incident.OccurredAt && x.CreatedAt >= incident.OccurredAt.AddHours(-24) && (x.ActionType.Contains("Test") || x.ActionType.Contains("Build") || x.ActionType.Contains("Execution"))).OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(ct);
        foreach (var log in logs) AddEvidence(incident, log.ActionType.Contains("Test") ? DebugEvidenceKind.TestResult : DebugEvidenceKind.Log, DebugEvidenceConfidence.Medium, log.ActionType, log.Description, null, null, null, log.CreatedAt);
        incident.RootCause = $"The execution failed because: {incident.ErrorSummary}";
        incident.LikelyRegression ??= "No evidence-backed regression commit was identified.";
        incident.SuggestedFix = "Inspect the reported error and the affected file versions, apply the smallest correction, then run the same command again.";
        if (useModel)
        {
            try
            {
                var evidence=incident.Evidence.OrderByDescending(x=>x.Confidence).Take(60).Select(x=>$"[{x.Kind}/{x.Confidence}] {x.Label}: {x.Summary}");
                var prompt=redaction.Redact($"Error: {incident.ErrorSummary}\nStack trace: {incident.StackTrace ?? "not available"}\nEvidence:\n{string.Join('\n',evidence)}\nSupported relevant commit: {incident.RelevantCommitSha ?? "none"}. Return JSON only: {{\"rootCause\":\"...\",\"suggestedFix\":\"...\"}}. Use only supplied evidence; say unknown where evidence is insufficient.");
                var output=new StringBuilder(); var request=new AiRequest("You are an evidence-constrained debugging analyst. Never invent commits, files, tests, logs, or causal claims. Do not ask for or infer repository source. Output exactly one JSON object.",prompt,string.Empty,incident.Language,AiAssistantAction.FindBug,[],MaxOutputTokens:900);
                using var modelTimeout=CancellationTokenSource.CreateLinkedTokenSource(ct); modelTimeout.CancelAfter(TimeSpan.FromSeconds(60));
                await foreach(var chunk in provider.StreamAsync(request,modelTimeout.Token).WithCancellation(modelTimeout.Token)) if(!chunk.IsCompleted&&output.Length<8000) output.Append(chunk.Content.AsSpan(0,Math.Min(chunk.Content.Length,8000-output.Length)));
                var json=ExtractJson(output.ToString()); using var document=JsonDocument.Parse(json); var root=document.RootElement;
                if(root.TryGetProperty("rootCause",out var cause)&&cause.ValueKind==JsonValueKind.String) incident.RootCause=Limit(cause.GetString(),4000);
                if(root.TryGetProperty("suggestedFix",out var fix)&&fix.ValueKind==JsonValueKind.String) incident.SuggestedFix=Limit(fix.GetString(),4000);
                incident.ModelProvider=provider.ProviderName; incident.ModelName=provider.Model;
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(OperationCanceledException exception){logger.LogWarning(exception,"Ollama debugging analysis timed out for incident {IncidentId}.",incident.ID);}
            catch(Exception exception){logger.LogWarning(exception,"Ollama debugging analysis unavailable for incident {IncidentId}.",incident.ID);}
        }
        incident.Status = DebuggingIncidentStatus.Analyzed; incident.AnalyzedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entries = string.Join(", ", exception.Entries.Select(entry =>
                $"{entry.Metadata.ClrType.Name}:{entry.State}:{string.Join('/', entry.Properties.Where(property => property.Metadata.IsPrimaryKey()).Select(property => property.CurrentValue))}:" +
                string.Join('|', entry.Properties.Where(property => property.IsModified).Select(property => $"{property.Metadata.Name}={property.OriginalValue}->{property.CurrentValue}"))));
            logger.LogError(exception, "Debugging analysis concurrency failure for incident {IncidentId}. Entries: {Entries}", incidentId, entries);
            throw;
        }
        return await Map(projectId, incidentId, ct);
    }

    private async Task<DebuggingIncidentDto> Map(Guid projectId, Guid id, CancellationToken ct)
    {
        var x = await db.DebuggingIncidents.AsNoTracking().Include(x => x.Evidence).SingleOrDefaultAsync(x => x.ID == id && x.ProjectId == projectId, ct) ?? throw new NotFoundException("Debugging incident not found.");
        return new(x.ID,x.ProjectId,x.WorkspaceNodeId,x.Kind,x.Status,x.Language,x.ErrorSummary,x.StackTrace,x.Stdout,x.Stderr,x.ExitCode,x.TimedOut,x.DurationMs,x.OccurredAt,x.AnalyzedAt,x.RootCause,x.LikelyRegression,x.SuggestedFix,x.RelevantCommitSha,x.RegressionConfidence,x.ModelProvider,x.ModelName,x.Evidence.OrderByDescending(e=>e.Confidence).ThenByDescending(e=>e.EvidenceAt).Select(e=>new DebugEvidenceDto(e.ID,e.Kind,e.Confidence,e.Label,e.Summary,e.WorkspaceNodeId,e.FileVersionId,e.CommitSha,e.EvidenceAt)).ToArray());
    }
    private async Task PruneObservations(Guid projectId, CancellationToken ct)
    {
        const int keep = 500;
        var cutoff = await db.DebuggingExecutionObservations.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.ExecutedAt).Skip(keep).Select(x => (DateTime?)x.ExecutedAt).FirstOrDefaultAsync(ct);
        if (cutoff.HasValue) await db.DebuggingExecutionObservations.Where(x => x.ProjectId == projectId && x.Succeeded && x.ExecutedAt <= cutoff.Value).ExecuteDeleteAsync(ct);
    }
    private void AddEvidence(DebuggingIncident incident, DebugEvidenceKind kind, DebugEvidenceConfidence confidence, string label, string summary, Guid? nodeId, Guid? versionId, string? sha, DateTime? at)
    {
        var fingerprint = Hash($"{kind}|{nodeId}|{versionId}|{sha}|{summary}"); if (incident.Evidence.Any(x => x.Fingerprint == fingerprint)) return;
        var evidence = new DebuggingEvidence { ID=Guid.NewGuid(), Kind=kind, Confidence=confidence, Label=Limit(label,200), Summary=Limit(summary,4000), WorkspaceNodeId=nodeId, FileVersionId=versionId, CommitSha=sha, EvidenceAt=at, Fingerprint=fingerprint, CreatAt=DateTime.UtcNow };
        incident.Evidence.Add(evidence);
        // A non-default Guid on a child discovered through an already tracked collection is
        // interpreted as an existing row by EF. Mark it explicitly so analysis inserts evidence.
        if (db.Entry(incident).State is not (EntityState.Detached or EntityState.Added)) db.DebuggingEvidence.Add(evidence);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));
    private static string Limit(string? value, int max) => (value ?? string.Empty).Length <= max ? value ?? string.Empty : value![..max];
    private static string? FirstMeaningful(string value) => value.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0);
    private static string Relative(DateTime before, DateTime after) { var span=after-before; return span.TotalMinutes<2 ? "less than two minutes" : span.TotalHours<2 ? $"{Math.Max(2,(int)span.TotalMinutes)} minutes" : span.TotalDays<2 ? $"{Math.Max(2,(int)span.TotalHours)} hours" : $"{Math.Max(2,(int)span.TotalDays)} days"; }
    private async Task<string> WorkspacePath(Guid projectId, Guid nodeId, CancellationToken ct)
    {
        var nodes=await db.WorkspaceNodes.AsNoTracking().Where(x=>x.ProjectId==projectId).Select(x=>new{x.ID,x.ParentId,x.Name}).ToListAsync(ct); var byId=nodes.ToDictionary(x=>x.ID); if(!byId.TryGetValue(nodeId,out var current)) throw new NotFoundException("Workspace file not found."); var parts=new List<string>(); var seen=new HashSet<Guid>(); while(true){if(!seen.Add(current.ID)) throw new ConflictException("The workspace tree contains a parent cycle."); parts.Add(current.Name); if(!current.ParentId.HasValue||!byId.TryGetValue(current.ParentId.Value,out var parent)) break; current=parent;} parts.Reverse(); return string.Join('/',parts);
    }
    private static string ExtractJson(string value){var start=value.IndexOf('{');var end=value.LastIndexOf('}');if(start<0||end<=start)throw new JsonException("Model response did not contain a JSON object.");return value[start..(end+1)];}
}

public sealed class ListDebuggingTimelineHandler(IDebuggingTimelineService service) : MediatR.IRequestHandler<ListDebuggingTimelineQuery,DebuggingTimelineDto> { public Task<DebuggingTimelineDto> Handle(ListDebuggingTimelineQuery r,CancellationToken ct)=>service.ListAsync(r.ProjectId,r.Take,ct); }
public sealed class GetDebuggingIncidentHandler(IDebuggingTimelineService service) : MediatR.IRequestHandler<GetDebuggingIncidentQuery,DebuggingIncidentDto> { public Task<DebuggingIncidentDto> Handle(GetDebuggingIncidentQuery r,CancellationToken ct)=>service.GetAsync(r.ProjectId,r.IncidentId,ct); }
public sealed class AnalyzeDebuggingIncidentHandler(IDebuggingTimelineService service) : MediatR.IRequestHandler<AnalyzeDebuggingIncidentCommand,DebuggingIncidentDto> { public Task<DebuggingIncidentDto> Handle(AnalyzeDebuggingIncidentCommand r,CancellationToken ct)=>service.AnalyzeAsync(r.ProjectId,r.IncidentId,r.UseModel,ct); }
