using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coding.Application.Abstractions;
using Coding.Application.Features.AiAssistant;
using Coding.Application.Features.AutonomousTesting;
using Coding.Application.Features.Debugging;
using Coding.Application.Features.FileExplorer;
using Coding.Application.Features.Runtime;
using Coding.Application.Features.Notifications;
using Coding.Application.Features.AiAgent;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.Projects;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.AutonomousTesting;

public sealed class AutonomousTestAgentService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAiProvider provider,
    IRuntimeProvider runtime,
    IDebuggingTimelineService debugging,
    ISender sender,
    INotificationService notifications,
    IAiSecretRedactionService redaction,
    ILogger<AutonomousTestAgentService> logger) : IAutonomousTestAgentService
{
    private const int MaximumSourceCharacters = 64_000;
    private const int MaximumGeneratedTestCharacters = 80_000;

    public async Task<AutonomousTestRunDto> StartAsync(StartAutonomousTestRunCommand request, CancellationToken ct)
    {
        await ProjectAccess.RequireWorkspaceWriteAsync(db, request.ProjectId, currentUser.UserId, ct);
        var maximumIterations = AutonomousTestPolicy.ClampIterations(request.MaximumIterations);
        var file = await db.WorkspaceNodes.AsNoTracking()
            .Where(x => x.ID == request.WorkspaceNodeId && x.ProjectId == request.ProjectId && x.NodeType == WorkspaceNodeType.File)
            .Select(x => new { x.ID, x.Name, Content = x.FileContent! })
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Workspace source file not found.");
        if (file.Content.IsBinary) throw new ConflictException("Binary files cannot be tested by the autonomous test agent.");
        if (!file.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("The isolated autonomous test runner currently supports C# files only.");
        if (file.Content.Content.Length > MaximumSourceCharacters)
            throw new ConflictException($"The source file exceeds the {MaximumSourceCharacters:N0}-character autonomous-test limit.");
        if (redaction.Redact(file.Content.Content) != file.Content.Content)
            throw new ConflictException("Autonomous testing stopped because the source contains secret-shaped data. Move secrets to protected configuration before sending code to AI.");

        var now = DateTime.UtcNow;
        var run = new AutonomousTestRun
        {
            ID = Guid.NewGuid(), ProjectId = request.ProjectId, UserId = currentUser.UserId,
            WorkspaceNodeId = file.ID, Goal = Limit(redaction.Redact(request.Goal.Trim()), 2000), Language = "csharp",
            Status = AutonomousTestRunStatus.Analyzing, MaximumIterations = maximumIterations,
            OriginalSourceHash = file.Content.ContentHash, OriginalConcurrencyToken = file.Content.ConcurrencyToken,
            ModelProvider = provider.ProviderName, ModelName = provider.Model, StartedAt = now, CreatAt = now
        };
        if (string.IsNullOrWhiteSpace(run.Goal)) throw new ArgumentException("A testing goal is required.");
        db.AutonomousTestRuns.Add(run);
        await db.SaveChangesAsync(ct);

        try
        {
            await ExecuteLoop(run, file.Content.Content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            run.Status = AutonomousTestRunStatus.Cancelled;
            run.FinalSummary = "The autonomous test run was cancelled.";
            run.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Autonomous test run {RunId} failed safely.", run.ID);
            db.ChangeTracker.Clear();
            var failedRun = await db.AutonomousTestRuns.SingleAsync(x => x.ID == run.ID, CancellationToken.None);
            failedRun.Status = AutonomousTestRunStatus.Failed;
            failedRun.FinalSummary = Limit($"The test agent stopped with evidence: {exception.Message}", 4000);
            failedRun.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var result=await Map(request.ProjectId, run.ID, ct);
        await notifications.CreateAsync(new(currentUser.UserId,NotificationType.AITask,"Autonomous test finished",$"Test run completed with status {result.Status}.",run.ID,nameof(AutonomousTestRun),$"autonomous-test:{run.ID:N}"),ct);
        return result;
    }

    public async Task<AutonomousTestRunDto> GetAsync(Guid projectId, Guid runId, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, ct);
        return await Map(projectId, runId, ct);
    }

    public async Task<AutonomousTestTimelineDto> ListAsync(Guid projectId, int take, CancellationToken ct)
    {
        await ProjectAccess.RequireMemberAsync(db, projectId, currentUser.UserId, ct);
        take = Math.Clamp(take, 1, 100);
        var total = await db.AutonomousTestRuns.CountAsync(x => x.ProjectId == projectId, ct);
        var ids = await db.AutonomousTestRuns.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.StartedAt).Take(take).Select(x => x.ID).ToListAsync(ct);
        var items = new List<AutonomousTestRunDto>(ids.Count);
        foreach (var id in ids) items.Add(await Map(projectId, id, ct));
        return new(items, total);
    }

    public async Task<AutonomousTestRunDto> ApplyAsync(Guid projectId, Guid runId, bool confirm, CancellationToken ct)
    {
        if (!confirm) throw new ArgumentException("Explicit confirmation is required before applying an AI-proposed fix.");
        await ProjectAccess.RequireWorkspaceWriteAsync(db, projectId, currentUser.UserId, ct);
        var run = await db.AutonomousTestRuns.SingleOrDefaultAsync(x => x.ID == runId && x.ProjectId == projectId, ct)
            ?? throw new NotFoundException("Autonomous test run not found.");
        if (run.Status != AutonomousTestRunStatus.AwaitingApply || string.IsNullOrWhiteSpace(run.ProposedSource))
            throw new ConflictException("This run has no validated fix awaiting approval.");
        if (string.IsNullOrWhiteSpace(run.ProposedSourceHash) || !string.Equals(Hash(run.ProposedSource), run.ProposedSourceHash, StringComparison.Ordinal))
            throw new ConflictException("The proposed fix failed its integrity check. Start a new test run.");
        var state = await db.FileContents.AsNoTracking().SingleAsync(x => x.NodeId == run.WorkspaceNodeId, ct);
        if (!string.Equals(state.ContentHash, run.OriginalSourceHash, StringComparison.Ordinal) ||
            !string.Equals(state.ConcurrencyToken, run.OriginalConcurrencyToken, StringComparison.Ordinal))
            throw new ConflictException("The source file changed after this run. Start a new test run before applying the fix.");

        var saved = await sender.Send(new SaveFileContentCommand(run.WorkspaceNodeId, run.ProposedSource, run.OriginalConcurrencyToken), ct);
        run.Status = AutonomousTestRunStatus.AppliedAwaitingRerun;
        run.AppliedAt = DateTime.UtcNow;
        run.AppliedFileVersionId = await db.FileVersions.AsNoTracking().Where(x => x.NodeId == run.WorkspaceNodeId && x.ContentHash == saved.ContentHash)
            .OrderByDescending(x => x.VersionNumber).Select(x => (Guid?)x.ID).FirstOrDefaultAsync(ct);
        run.UpdateAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await Map(projectId, runId, ct);
    }

    public async Task<AutonomousTestRunDto> RunAgainAsync(Guid projectId, Guid runId, int maximumIterations, CancellationToken ct)
    {
        await ProjectAccess.RequireWorkspaceWriteAsync(db, projectId, currentUser.UserId, ct);
        var prior = await db.AutonomousTestRuns.AsNoTracking().SingleOrDefaultAsync(x => x.ID == runId && x.ProjectId == projectId, ct)
            ?? throw new NotFoundException("Autonomous test run not found.");
        return await StartAsync(new(projectId, prior.WorkspaceNodeId, $"Re-run after {runId}: {prior.Goal}", maximumIterations), ct);
    }

    private async Task ExecuteLoop(AutonomousTestRun run, string originalSource, CancellationToken ct)
    {
        var candidateSource = originalSource;
        string? previousFailure = null;
        for (var number = 1; number <= run.MaximumIterations; number++)
        {
            run.Status = AutonomousTestRunStatus.Running;
            var startedAt = DateTime.UtcNow;
            ModelIteration model;
            try
            {
                model = await GenerateIteration(run, candidateSource, previousFailure, ct);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException ||
                                               exception is OperationCanceledException && !ct.IsCancellationRequested)
            {
                var invalid = new AutonomousTestIteration
                {
                    ID = Guid.NewGuid(), Number = number, Outcome = AutonomousTestIterationOutcome.InvalidModelOutput,
                    SourceHash = Hash(candidateSource), GeneratedTestSource = string.Empty,
                    FailureAnalysis = Limit(exception.Message, 4000), StartedAt = startedAt, CompletedAt = DateTime.UtcNow, CreatAt = startedAt
                };
                run.Iterations.Add(invalid);
                db.AutonomousTestIterations.Add(invalid);
                run.CompletedIterations = number;
                run.Status = AutonomousTestRunStatus.Failed;
                run.FinalSummary = "Tests failed with evidence: Ollama did not return a valid bounded test program.";
                run.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            run.Analysis ??= model.Analysis;
            RuntimeExecutionResult execution;
            try
            {
                var executionSource = candidateSource + "\n\n" + model.GeneratedTestSource;
                execution = await runtime.ExecuteAsync(new RuntimeExecutionRequest("csharp", executionSource, 15, "AutonomousTestRunner"), ct);
            }
            catch (InvalidOperationException exception)
            {
                var unavailable = new AutonomousTestIteration
                {
                    ID = Guid.NewGuid(), Number = number, Outcome = AutonomousTestIterationOutcome.RuntimeUnavailable,
                    SourceHash = Hash(candidateSource), GeneratedTestSource = model.GeneratedTestSource,
                    FailureAnalysis = Limit(exception.Message, 4000), StartedAt = startedAt,
                    CompletedAt = DateTime.UtcNow, CreatAt = startedAt
                };
                run.Iterations.Add(unavailable); db.AutonomousTestIterations.Add(unavailable);
                run.CompletedIterations = number; run.Status = AutonomousTestRunStatus.Failed;
                run.FinalSummary = "Tests failed with evidence: the isolated runtime was unavailable.";
                run.CompletedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return;
            }
            await debugging.CaptureFailureAsync(new DebugExecutionCapture(
                run.ProjectId, run.UserId, run.WorkspaceNodeId, DebuggingIncidentKind.Test, "csharp",
                candidateSource + "\n\n" + model.GeneratedTestSource, execution.ExitCode, execution.Stdout, execution.Stderr,
                execution.TimedOut, execution.DurationMs), ct);
            var passed = AutonomousTestPolicy.IsPassingExecution(execution.ExitCode, execution.TimedOut, execution.Stderr);
            var iteration = new AutonomousTestIteration
            {
                ID = Guid.NewGuid(), Number = number,
                Outcome = passed ? AutonomousTestIterationOutcome.Passed : execution.TimedOut ? AutonomousTestIterationOutcome.TimedOut : AutonomousTestIterationOutcome.Failed,
                SourceHash = Hash(candidateSource), GeneratedTestSource = model.GeneratedTestSource,
                Stdout = Limit(execution.Stdout, 32000), Stderr = Limit(execution.Stderr, 32000), ExitCode = execution.ExitCode,
                TimedOut = execution.TimedOut, DurationMs = execution.DurationMs,
                FailureAnalysis = passed ? null : Limit(string.IsNullOrWhiteSpace(model.FailureAnalysis) ? FirstLine(execution.Stderr) : model.FailureAnalysis, 4000),
                StartedAt = startedAt, CompletedAt = DateTime.UtcNow, CreatAt = startedAt
            };
            run.Iterations.Add(iteration);
            db.AutonomousTestIterations.Add(iteration);
            run.CompletedIterations = number;
            run.SuggestedFix = model.SuggestedFix;

            if (passed)
            {
                var proposed = string.IsNullOrWhiteSpace(model.ProposedSource) ? candidateSource : model.ProposedSource;
                if (!string.Equals(Hash(proposed), run.OriginalSourceHash, StringComparison.Ordinal))
                {
                    run.ProposedSource = proposed;
                    run.ProposedSourceHash = Hash(proposed);
                    run.Status = AutonomousTestRunStatus.AwaitingApply;
                    run.FinalSummary = "Tests passed against the proposed source in the isolated runner. The project file is unchanged until you explicitly apply the fix.";
                }
                else
                {
                    run.Status = AutonomousTestRunStatus.Passed;
                    run.FinalSummary = "Tests passed in the isolated runner; no source change is required.";
                }
                run.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            previousFailure = $"Exit code: {execution.ExitCode?.ToString() ?? "unknown"}; timed out: {execution.TimedOut}; stderr: {Limit(execution.Stderr, 8000)}; stdout: {Limit(execution.Stdout, 4000)}";
            if (!string.IsNullOrWhiteSpace(model.ProposedSource) && model.ProposedSource.Length <= MaximumSourceCharacters)
                candidateSource = model.ProposedSource;
            await db.SaveChangesAsync(ct);
        }

        run.Status = AutonomousTestRunStatus.Failed;
        run.FinalSummary = $"Tests failed with evidence after the maximum {run.MaximumIterations} iteration(s).";
        run.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<ModelIteration> GenerateIteration(AutonomousTestRun run, string source, string? previousFailure, CancellationToken ct)
    {
        var prompt = $$"""
            Goal: {{run.Goal}}
            Iteration: {{run.CompletedIterations + 1}} of {{run.MaximumIterations}}
            Previous execution evidence: {{previousFailure ?? "none; test the current source before suggesting a fix"}}

            Current C# source:
            --- source ---
            {{source}}
            --- end source ---

            Return these exact plain-text sections, with TEST_SOURCE first:
            <<<TEST_SOURCE>>>
            complete program
            <<<END_TEST_SOURCE>>>
            <<<ANALYSIS>>>brief analysis<<<END_ANALYSIS>>>
            <<<FAILURE_ANALYSIS>>>brief failure analysis or empty<<<END_FAILURE_ANALYSIS>>>
            <<<SUGGESTED_FIX>>>brief fix or empty<<<END_SUGGESTED_FIX>>>
            <<<PROPOSED_SOURCE>>>complete corrected original source or empty<<<END_PROPOSED_SOURCE>>>
            TEST_SOURCE must be only a no-NuGet C# test harness of at most 100 lines. Do not copy or redefine the supplied source. It must declare `class AutonomousTestRunner` with `public static void Main()`, call the supplied public code, throw on failed deterministic assertions, and print a concise summary. Keep every narrative section below 500 characters. No namespace declaration, network, files, environment access, sleeps, reflection, or process execution. On the first iteration PROPOSED_SOURCE should be empty unless supplied evidence already proves a defect. After execution failure evidence, it may contain the complete corrected original source. Never report tests as passed; the server runs them independently.
            """;
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var attemptPrompt = attempt == 1 ? prompt : prompt + "\nYour previous response was invalid. Start immediately with <<<TEST_SOURCE>>> and close it with <<<END_TEST_SOURCE>>> before any explanation.";
            var request = new AiRequest(
                "You are a bounded autonomous C# test engineer. Generate deterministic tests, preserve public behavior, use only supplied source/evidence, and never invent execution results. Follow the exact section markers.",
                attemptPrompt, string.Empty, "csharp", AiAssistantAction.GenerateTests, [], MaxOutputTokens: 500);
            var output = new StringBuilder();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            await foreach (var chunk in provider.StreamAsync(request, timeout.Token).WithCancellation(timeout.Token))
                if (!chunk.IsCompleted && output.Length < 120_000) output.Append(chunk.Content.AsSpan(0, Math.Min(chunk.Content.Length, 120_000 - output.Length)));
            try
            {
                var markedTest = ExtractSection(output.ToString(), "TEST_SOURCE");
                if (!string.IsNullOrWhiteSpace(markedTest))
                {
                    if (markedTest.Length > MaximumGeneratedTestCharacters) throw new InvalidOperationException("Generated test source exceeded the execution limit.");
                    if (!AutonomousTestPolicy.HasDedicatedRunner(markedTest)) throw new InvalidOperationException("Generated tests did not declare the required AutonomousTestRunner entrypoint.");
                    var markedProposed = ExtractSection(output.ToString(), "PROPOSED_SOURCE");
                    if (markedProposed?.Length > MaximumSourceCharacters) throw new InvalidOperationException("Proposed source exceeded the application limit.");
                    return new(Limit(ExtractSection(output.ToString(), "ANALYSIS") ?? "Ollama generated a bounded test program from the supplied source.", 4000), markedTest,
                        Limit(ExtractSection(output.ToString(), "FAILURE_ANALYSIS"), 4000), Limit(ExtractSection(output.ToString(), "SUGGESTED_FIX"), 4000), markedProposed);
                }
                var json = ExtractJson(output.ToString());
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var testSource = OptionalAny(root, "generatedTestSource", "testSource", "generatedTests", "tests")
                    ?? throw new JsonException($"Ollama response omitted the generated test source. Returned keys: {string.Join(", ", root.EnumerateObject().Select(property => property.Name).Take(20))}.");
                if (testSource.Length > MaximumGeneratedTestCharacters) throw new InvalidOperationException("Generated test source exceeded the execution limit.");
                if (!AutonomousTestPolicy.HasDedicatedRunner(testSource)) throw new InvalidOperationException("Generated tests did not declare the required AutonomousTestRunner entrypoint.");
                var proposed = OptionalAny(root, "proposedSource", "fixedSource", "correctedSource");
                if (proposed?.Length > MaximumSourceCharacters) throw new InvalidOperationException("Proposed source exceeded the application limit.");
                return new(Limit(OptionalAny(root, "analysis", "summary") ?? "Ollama generated a bounded test program from the supplied source.", 4000), testSource,
                    Limit(OptionalAny(root, "failureAnalysis", "failure"), 4000), Limit(OptionalAny(root, "suggestedFix", "fix"), 4000), proposed);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                lastError = exception;
            }
        }
        throw new InvalidOperationException($"Ollama returned invalid test output after two bounded attempts: {lastError?.Message}", lastError);
    }

    private async Task<AutonomousTestRunDto> Map(Guid projectId, Guid runId, CancellationToken ct)
    {
        var run = await db.AutonomousTestRuns.AsNoTracking().Include(x => x.Iterations)
            .SingleOrDefaultAsync(x => x.ID == runId && x.ProjectId == projectId, ct)
            ?? throw new NotFoundException("Autonomous test run not found.");
        return new(run.ID, run.ProjectId, run.WorkspaceNodeId, run.Goal, run.Language, run.Status,
            run.MaximumIterations, run.CompletedIterations, run.Analysis, run.FinalSummary, run.SuggestedFix,
            !string.IsNullOrWhiteSpace(run.ProposedSource), run.ProposedSource, run.ProposedSourceHash,
            run.ModelProvider, run.ModelName, run.StartedAt, run.CompletedAt, run.AppliedAt, run.AppliedFileVersionId,
            run.Iterations.OrderBy(x => x.Number).Select(x => new AutonomousTestIterationDto(x.ID, x.Number, x.Outcome,
                x.SourceHash, x.GeneratedTestSource, x.Stdout, x.Stderr, x.ExitCode, x.TimedOut, x.DurationMs,
                x.FailureAnalysis, x.StartedAt, x.CompletedAt)).ToArray());
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Limit(string? value, int maximum) => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maximum ? value : value[..maximum];
    private static string? FirstLine(string? value) => value?.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0);
    private static string ExtractJson(string value) { var start = value.IndexOf('{'); var end = value.LastIndexOf('}'); if (start < 0 || end <= start) throw new JsonException("Ollama response did not contain a JSON object."); return value[start..(end + 1)]; }
    private static string? ExtractSection(string value, string name) { var startMarker = $"<<<{name}>>>"; var endMarker = $"<<<END_{name}>>>"; var start = value.IndexOf(startMarker, StringComparison.Ordinal); if (start < 0) return null; start += startMarker.Length; var end = value.IndexOf(endMarker, start, StringComparison.Ordinal); return end < start ? null : value[start..end].Trim(); }
    private static string Required(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new JsonException($"Ollama response omitted '{name}'.");
    private static string? Optional(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
    private static string? OptionalAny(JsonElement root, params string[] names) => names.Select(name => Optional(root, name)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record ModelIteration(string Analysis, string GeneratedTestSource, string? FailureAnalysis, string? SuggestedFix, string? ProposedSource);
}

public sealed class StartAutonomousTestRunHandler(IAutonomousTestAgentService service) : IRequestHandler<StartAutonomousTestRunCommand, AutonomousTestRunDto>
{ public Task<AutonomousTestRunDto> Handle(StartAutonomousTestRunCommand request, CancellationToken ct) => service.StartAsync(request, ct); }
public sealed class GetAutonomousTestRunHandler(IAutonomousTestAgentService service) : IRequestHandler<GetAutonomousTestRunQuery, AutonomousTestRunDto>
{ public Task<AutonomousTestRunDto> Handle(GetAutonomousTestRunQuery request, CancellationToken ct) => service.GetAsync(request.ProjectId, request.RunId, ct); }
public sealed class ListAutonomousTestRunsHandler(IAutonomousTestAgentService service) : IRequestHandler<ListAutonomousTestRunsQuery, AutonomousTestTimelineDto>
{ public Task<AutonomousTestTimelineDto> Handle(ListAutonomousTestRunsQuery request, CancellationToken ct) => service.ListAsync(request.ProjectId, request.Take, ct); }
public sealed class ApplyAutonomousTestFixHandler(IAutonomousTestAgentService service) : IRequestHandler<ApplyAutonomousTestFixCommand, AutonomousTestRunDto>
{ public Task<AutonomousTestRunDto> Handle(ApplyAutonomousTestFixCommand request, CancellationToken ct) => service.ApplyAsync(request.ProjectId, request.RunId, request.Confirm, ct); }
public sealed class RunAutonomousTestsAgainHandler(IAutonomousTestAgentService service) : IRequestHandler<RunAutonomousTestsAgainCommand, AutonomousTestRunDto>
{ public Task<AutonomousTestRunDto> Handle(RunAutonomousTestsAgainCommand request, CancellationToken ct) => service.RunAgainAsync(request.ProjectId, request.RunId, request.MaximumIterations, ct); }
