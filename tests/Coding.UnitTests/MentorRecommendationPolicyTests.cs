using Coding.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class MentorRecommendationPolicyTests
{
    [Fact]
    public void Always_returns_the_five_required_growth_categories()
    {
        var result = MentorRecommendationPolicy.Build(new MentorEvidenceSnapshot([], [], [], 0, 0, 0, 0, false));

        result.Select(x => x.Category).Should().BeEquivalentTo(
            ["NextTechnology", "ProjectIdea", "MissingSkill", "TestingImprovement", "ArchitectureTopic"]);
    }

    [Fact]
    public void Uses_observed_testing_and_architecture_evidence()
    {
        var result = MentorRecommendationPolicy.Build(new MentorEvidenceSnapshot(["C#"], ["Architecture"], ["csharp"], 2, 4, 8, 3, true));

        result.Single(x => x.Category == "TestingImprovement").Rationale.Should().Contain("3");
        result.Single(x => x.Category == "ArchitectureTopic").Rationale.Should().Contain("Layered");
    }

    [Fact]
    public void Does_not_embed_sensitive_attribute_inferences()
    {
        var text = string.Join(' ', MentorRecommendationPolicy.Build(new MentorEvidenceSnapshot([], [], [], 0, 0, 0, 0, false))
            .SelectMany(x => new[] { x.Title, x.Rationale, x.Action }));

        text.Should().NotContainEquivalentOf("gender").And.NotContainEquivalentOf("ethnicity").And.NotContainEquivalentOf("health");
    }
}
