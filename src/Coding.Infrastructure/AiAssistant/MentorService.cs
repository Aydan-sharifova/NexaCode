using System.Text;
using Coding.Application.Abstractions;
using Coding.Application.Features.AiAssistant;
using Coding.Application.Features.Mentor;
using Coding.Data;
using Coding.Domain.Services;
using Coding.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.AiAssistant;

public sealed class MentorService(AppDbContext db, ICurrentUser currentUser, IAiProvider provider, ILogger<MentorService> logger) : IMentorService
{
    public async Task<MentorAnalysis> AnalyzeAsync(bool generateNarrative, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var profile = await db.DeveloperProfiles.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.Skills, x.LearningTopics }).SingleOrDefaultAsync(cancellationToken);
        var projectIds = db.Projects.AsNoTracking().Where(x => x.OwnerId == userId || x.Members.Any(m => m.UserId == userId)).Select(x => x.ID);
        var languages = await db.Projects.AsNoTracking().Where(x => projectIds.Contains(x.ID) && x.DefaultLanguage != "")
            .Select(x => x.DefaultLanguage).Distinct().OrderBy(x => x).Take(20).ToListAsync(cancellationToken);
        var projectCount = await projectIds.CountAsync(cancellationToken);
        var completedTasks = await db.TaskAssignees.AsNoTracking().CountAsync(x => x.UserId == userId && x.Task.Status == ProjectTaskStatus.Done && projectIds.Contains(x.Task.ProjectId), cancellationToken);
        var commits = await db.GitCommits.AsNoTracking().CountAsync(x => x.UserId == userId && projectIds.Contains(x.ProjectId), cancellationToken);
        var fileNames = await db.WorkspaceNodes.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId) && x.NodeType == WorkspaceNodeType.File)
            .OrderBy(x => x.ID).Select(x => x.Name).Take(5_000).ToListAsync(cancellationToken);
        var testCount = fileNames.Count(IsTestFile);
        var layered = fileNames.Any(x => x.Contains("Domain", StringComparison.OrdinalIgnoreCase)) &&
                      fileNames.Any(x => x.Contains("Application", StringComparison.OrdinalIgnoreCase) || x.Contains("Service", StringComparison.OrdinalIgnoreCase));
        var snapshot = new MentorEvidenceSnapshot(profile?.Skills ?? [], profile?.LearningTopics ?? [], languages, projectCount, completedTasks, commits, testCount, layered);
        var recommendations = MentorRecommendationPolicy.Build(snapshot)
            .Select(x => new MentorRecommendation(x.Category, x.Title, x.Rationale, x.Action)).ToArray();
        var evidence = new MentorEvidence(snapshot.DeclaredSkills, snapshot.LearningTopics, snapshot.ObservedTechnologies, projectCount, completedTasks, commits, testCount, layered, DateTime.UtcNow);
        if (!generateNarrative)
            return Result(evidence, recommendations, null, false);

        try
        {
            var narrative = new StringBuilder();
            var request = new AiRequest(
                "You are a privacy-safe developer mentor. Use only the supplied aggregate evidence. Do not infer age, gender, ethnicity, health, politics, religion, finances, location, personality, or other sensitive personal attributes. Do not invent projects, skills, outcomes, or experience. Give a concise growth summary and explain the five supplied recommendations without changing their evidence.",
                BuildPrompt(evidence, recommendations), string.Empty, "general", AiAssistantAction.Chat, [], MaxOutputTokens: 900);
            await foreach (var chunk in provider.StreamAsync(request, cancellationToken).WithCancellation(cancellationToken))
            {
                if (!chunk.IsCompleted && narrative.Length < 8_000) narrative.Append(chunk.Content.AsSpan(0, Math.Min(chunk.Content.Length, 8_000 - narrative.Length)));
            }
            var text = narrative.ToString().Trim();
            return Result(evidence, recommendations, text.Length == 0 ? null : text, text.Length > 0);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Ollama mentor narrative was unavailable for user {UserId}.", userId);
            return Result(evidence, recommendations, null, false);
        }
    }

    private MentorAnalysis Result(MentorEvidence evidence, IReadOnlyList<MentorRecommendation> recommendations, string? narrative, bool available) =>
        new(evidence, recommendations, narrative, provider.ProviderName, provider.Model, available,
            "Mentor analysis uses only your authorized development activity and declared learning data. Sensitive personal attributes are never inferred.");

    private static string BuildPrompt(MentorEvidence evidence, IReadOnlyList<MentorRecommendation> recommendations) =>
        $"Evidence: declared skills [{string.Join(", ", evidence.DeclaredSkills)}]; learning topics [{string.Join(", ", evidence.LearningTopics)}]; observed technologies [{string.Join(", ", evidence.ObservedTechnologies)}]; projects {evidence.ProjectCount}; completed assigned tasks {evidence.CompletedTaskCount}; commits {evidence.CommitCount}; test-like files {evidence.TestFileCount}; layered architecture {evidence.UsesLayeredArchitecture}.\nRecommendations:\n" +
        string.Join("\n", recommendations.Select(x => $"- {x.Category}: {x.Title}. {x.Rationale} Action: {x.Action}"));

    private static bool IsTestFile(string name) => name.Contains("test", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("spec", StringComparison.OrdinalIgnoreCase);
}

public sealed class GetMentorAnalysisHandler(IMentorService mentor) : MediatR.IRequestHandler<GetMentorAnalysisQuery, MentorAnalysis>
{
    public Task<MentorAnalysis> Handle(GetMentorAnalysisQuery request, CancellationToken cancellationToken) => mentor.AnalyzeAsync(false, cancellationToken);
}

public sealed class GenerateMentorAnalysisHandler(IMentorService mentor) : MediatR.IRequestHandler<GenerateMentorAnalysisCommand, MentorAnalysis>
{
    public Task<MentorAnalysis> Handle(GenerateMentorAnalysisCommand request, CancellationToken cancellationToken) => mentor.AnalyzeAsync(true, cancellationToken);
}
