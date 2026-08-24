using Coding.Application.Features.PullRequests;
using Coding.Enums;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class PullRequestMergeRulesTests
{
    [Fact]
    public void Satisfied_open_pull_request_is_mergeable()
    {
        PullRequestMergeRules.Evaluate(PullRequestStatus.Open, 2, 2, false, false, true, true)
            .Should().BeEmpty();
    }

    [Fact]
    public void Every_protection_gate_reports_a_reason()
    {
        var reasons = PullRequestMergeRules.Evaluate(PullRequestStatus.Closed, 0, 2, true, true, true, false);

        reasons.Should().HaveCount(5);
        reasons.Should().Contain(reason => reason.Contains("not open"));
        reasons.Should().Contain(reason => reason.Contains("2 required approval"));
        reasons.Should().Contain(reason => reason.Contains("requested changes"));
        reasons.Should().Contain(reason => reason.Contains("Blocking"));
        reasons.Should().Contain(reason => reason.Contains("tests"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void Required_tests_must_explicitly_pass(bool? result)
    {
        PullRequestMergeRules.Evaluate(PullRequestStatus.Open, 1, 1, false, false, true, result)
            .Should().ContainSingle(reason => reason.Contains("tests"));
    }
}
