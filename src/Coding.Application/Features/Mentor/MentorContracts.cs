using MediatR;

namespace Coding.Application.Features.Mentor;

public sealed record MentorEvidence(
    IReadOnlyList<string> DeclaredSkills,
    IReadOnlyList<string> LearningTopics,
    IReadOnlyList<string> ObservedTechnologies,
    int ProjectCount,
    int CompletedTaskCount,
    int CommitCount,
    int TestFileCount,
    bool UsesLayeredArchitecture,
    DateTime AnalyzedAt);

public sealed record MentorRecommendation(string Category, string Title, string Rationale, string Action);
public sealed record MentorAnalysis(
    MentorEvidence Evidence,
    IReadOnlyList<MentorRecommendation> Recommendations,
    string? ModelNarrative,
    string Provider,
    string? Model,
    bool ModelAvailable,
    string PrivacyNotice);

public sealed record GetMentorAnalysisQuery : IRequest<MentorAnalysis>;
public sealed record GenerateMentorAnalysisCommand : IRequest<MentorAnalysis>;
public interface IMentorService
{
    Task<MentorAnalysis> AnalyzeAsync(bool generateNarrative, CancellationToken cancellationToken);
}
