using Coding.Application.Features.Moderation;
using Coding.Domain.Services;
using Coding.Enums;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class ModerationPolicyTests
{
    [Theory]
    [InlineData(ModerationReportState.Pending, ModerationActionType.StartReview, ModerationReportState.Reviewing)]
    [InlineData(ModerationReportState.Reviewing, ModerationActionType.Dismiss, ModerationReportState.Dismissed)]
    [InlineData(ModerationReportState.Reviewing, ModerationActionType.RemoveContent, ModerationReportState.ActionTaken)]
    [InlineData(ModerationReportState.Reviewing, ModerationActionType.SuspendProfile, ModerationReportState.ActionTaken)]
    [InlineData(ModerationReportState.Reviewing, ModerationActionType.RestoreToPending, ModerationReportState.Pending)]
    public void Allows_only_explicit_moderation_transitions(ModerationReportState state, ModerationActionType action, ModerationReportState expected) => ModerationLifecycle.Next(state, action).Should().Be(expected);

    [Theory]
    [InlineData(ModerationReportState.Pending, ModerationActionType.RemoveContent)]
    [InlineData(ModerationReportState.ActionTaken, ModerationActionType.StartReview)]
    [InlineData(ModerationReportState.Dismissed, ModerationActionType.RestoreToPending)]
    public void Terminal_or_unreviewed_reports_cannot_skip_the_workflow(ModerationReportState state, ModerationActionType action) => ModerationLifecycle.Next(state, action).Should().BeNull();

    [Fact]
    public void Other_reason_requires_details()
    {
        new CreateContentReportValidator().Validate(new CreateContentReportCommand(ReportTargetType.Post, Guid.NewGuid(), "Other", null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Arbitrary_reason_is_rejected()
    {
        new CreateContentReportValidator().Validate(new CreateContentReportCommand(ReportTargetType.Post, Guid.NewGuid(), "I disagree", "text")).IsValid.Should().BeFalse();
    }
}
