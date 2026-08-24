using Coding.Application.Features.LiveRooms;
using Coding.Domain.Services;
using Coding.Enums;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class LiveRoomPolicyTests
{
    [Theory]
    [InlineData(LiveRoomStatus.Scheduled, LiveRoomStatus.Active)]
    [InlineData(LiveRoomStatus.Scheduled, LiveRoomStatus.Cancelled)]
    [InlineData(LiveRoomStatus.Active, LiveRoomStatus.Completed)]
    [InlineData(LiveRoomStatus.Active, LiveRoomStatus.Cancelled)]
    public void Allows_only_forward_terminal_transitions(LiveRoomStatus current, LiveRoomStatus next) => LiveRoomLifecycle.CanTransition(current, next).Should().BeTrue();

    [Theory]
    [InlineData(LiveRoomStatus.Completed, LiveRoomStatus.Active)]
    [InlineData(LiveRoomStatus.Cancelled, LiveRoomStatus.Active)]
    [InlineData(LiveRoomStatus.Scheduled, LiveRoomStatus.Completed)]
    [InlineData(LiveRoomStatus.Active, LiveRoomStatus.Scheduled)]
    public void Rejects_invalid_or_reopening_transitions(LiveRoomStatus current, LiveRoomStatus next) => LiveRoomLifecycle.CanTransition(current, next).Should().BeFalse();

    [Theory]
    [InlineData(LiveRoomParticipantRole.Owner, true)]
    [InlineData(LiveRoomParticipantRole.Host, true)]
    [InlineData(LiveRoomParticipantRole.Interviewer, true)]
    [InlineData(LiveRoomParticipantRole.Candidate, false)]
    [InlineData(LiveRoomParticipantRole.Participant, false)]
    public void Management_roles_are_explicit(LiveRoomParticipantRole role, bool expected) => LiveRoomLifecycle.CanManage(role).Should().Be(expected);

    [Fact]
    public void Interview_requires_challenge_type()
    {
        var validator = new CreateLiveRoomValidator();
        var command = new CreateLiveRoomCommand(null, "Interview", null, LiveRoomMode.Interview, LiveRoomVisibility.InviteOnly, null, null, null, 60, DateTime.UtcNow.AddHours(1));
        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Room_duration_is_bounded()
    {
        var validator = new CreateLiveRoomValidator();
        var command = new CreateLiveRoomCommand(null, "Workshop", null, LiveRoomMode.Workshop, LiveRoomVisibility.InviteOnly, null, null, null, 1000, DateTime.UtcNow.AddHours(1));
        var result = validator.Validate(command);
        result.Errors.Should().Contain(error => error.PropertyName == "DurationMinutes");
    }

    [Theory]
    [InlineData("👍", true)]
    [InlineData("🚀", true)]
    [InlineData("not-an-emoji", false)]
    [InlineData("🔥", false)]
    public void Room_reactions_use_a_small_allowlist(string emoji, bool expected)
    {
        new AddRoomReactionValidator().Validate(new AddRoomReactionCommand(Guid.NewGuid(), emoji)).IsValid.Should().Be(expected);
    }

    [Fact]
    public void Private_interviewer_note_content_is_bounded()
    {
        var command = new SaveInterviewerNoteCommand(Guid.NewGuid(), null, new string('x', 8001));
        new SaveInterviewerNoteValidator().Validate(command).IsValid.Should().BeFalse();
    }
}
