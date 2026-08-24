namespace Coding.Domain.Services;

public sealed record MentorEvidenceSnapshot(
    IReadOnlyList<string> DeclaredSkills,
    IReadOnlyList<string> LearningTopics,
    IReadOnlyList<string> ObservedTechnologies,
    int ProjectCount,
    int CompletedTaskCount,
    int CommitCount,
    int TestFileCount,
    bool UsesLayeredArchitecture);

public sealed record MentorPolicyRecommendation(string Category, string Title, string Rationale, string Action);

public static class MentorRecommendationPolicy
{
    public static IReadOnlyList<MentorPolicyRecommendation> Build(MentorEvidenceSnapshot evidence)
    {
        var technologies = evidence.ObservedTechnologies
            .Concat(evidence.DeclaredSkills)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var nextTechnology = technologies.Any(IsFrontend)
            ? new MentorPolicyRecommendation("NextTechnology", "Add a typed backend boundary", "Your evidence is stronger on client-side technologies.", "Build one small authenticated API and connect it to an existing project.")
            : new MentorPolicyRecommendation("NextTechnology", "Strengthen the client boundary", "Your evidence is stronger on backend or general-purpose technologies.", "Add a typed UI client with loading, empty and failure states.");
        var projectIdea = evidence.ProjectCount == 0
            ? new MentorPolicyRecommendation("ProjectIdea", "Ship a focused starter project", "No persisted project evidence is available yet.", "Create a small project with one user workflow, tests and a README.")
            : new MentorPolicyRecommendation("ProjectIdea", "Turn one project into a production case study", $"You have {evidence.ProjectCount} persisted project(s).", "Choose one project and add observability, deployment notes and an architecture decision record.");
        var missingSkill = evidence.CompletedTaskCount == 0
            ? new MentorPolicyRecommendation("MissingSkill", "Practice delivery decomposition", "No completed assigned task is recorded.", "Break one feature into three measurable tasks and complete them through the board.")
            : new MentorPolicyRecommendation("MissingSkill", "Deepen review and collaboration", $"You have completed {evidence.CompletedTaskCount} assigned task(s).", "Open a pull request and request evidence-based review from a collaborator.");
        var testing = evidence.TestFileCount == 0
            ? new MentorPolicyRecommendation("TestingImprovement", "Create the first automated test boundary", "No test file is visible in your authorized project workspaces.", "Add one happy-path test and one failure-path test for the most important behavior.")
            : new MentorPolicyRecommendation("TestingImprovement", "Measure meaningful test gaps", $"Your workspaces contain {evidence.TestFileCount} test-like file(s).", "Add boundary and authorization tests around the highest-risk workflow.");
        var architecture = evidence.UsesLayeredArchitecture
            ? new MentorPolicyRecommendation("ArchitectureTopic", "Study dependency direction and module boundaries", "Layered project structure is visible in authorized workspace paths.", "Document one dependency rule and enforce it with an architecture test.")
            : new MentorPolicyRecommendation("ArchitectureTopic", "Introduce explicit application boundaries", "No layered structure is visible in the available workspace evidence.", "Separate domain behavior, application orchestration and external infrastructure in one feature slice.");
        return [nextTechnology, projectIdea, missingSkill, testing, architecture];
    }

    private static bool IsFrontend(string value) => value.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("typescript", StringComparison.OrdinalIgnoreCase) || value.Contains("react", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("vue", StringComparison.OrdinalIgnoreCase) || value.Contains("html", StringComparison.OrdinalIgnoreCase);
}
