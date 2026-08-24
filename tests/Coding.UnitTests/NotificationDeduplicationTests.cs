using Coding.Application.Features.Notifications;
using Coding.Enums;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class NotificationDeduplicationTests
{
    [Fact] public void Same_event_in_same_minute_has_same_key()
    {
        var request=new CreateNotificationRequest(Guid.NewGuid(),NotificationType.PostLike,"Liked","Someone liked your post",Guid.NewGuid(),"Post");
        NotificationDeduplication.Key(request,new DateTime(2026,8,23,8,10,1,DateTimeKind.Utc)).Should().Be(NotificationDeduplication.Key(request,new DateTime(2026,8,23,8,10,59,DateTimeKind.Utc)));
    }
    [Fact] public void Different_minute_or_explicit_event_key_is_distinct_and_bounded()
    {
        var request=new CreateNotificationRequest(Guid.NewGuid(),NotificationType.AgentCompletion,"Done","Agent completed",Guid.NewGuid(),"AgentRun");
        var first=NotificationDeduplication.Key(request,new DateTime(2026,8,23,8,10,59,DateTimeKind.Utc));
        first.Should().HaveLength(64).And.NotBe(NotificationDeduplication.Key(request,new DateTime(2026,8,23,8,11,0,DateTimeKind.Utc)));
        NotificationDeduplication.Key(request with{DeduplicationKey="run:stable"},DateTime.UtcNow).Should().Be(NotificationDeduplication.Key(request with{DeduplicationKey="run:stable"},DateTime.UtcNow.AddYears(1)));
    }
}
