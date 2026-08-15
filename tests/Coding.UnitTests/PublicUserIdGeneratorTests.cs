using Coding.Infrastructure.Users;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class PublicUserIdGeneratorTests
{
    [Fact]
    public void Candidate_uses_short_unambiguous_public_format()
    {
        var candidate = PublicUserIdGenerator.CreateCandidate();

        candidate.Should().MatchRegex("^[A-HJ-NP-Z2-9]{8}$");
        candidate.Should().NotContainAny("0", "O", "1", "I");
    }

    [Fact]
    public void Candidates_are_collision_resistant_in_a_representative_sample()
    {
        var candidates = Enumerable.Range(0, 10_000)
            .Select(_ => PublicUserIdGenerator.CreateCandidate())
            .ToArray();

        candidates.Distinct().Should().HaveCount(candidates.Length);
    }
}
