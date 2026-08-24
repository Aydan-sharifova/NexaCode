using Coding.Enums;

namespace Coding.Domain.Services;

public static class ModerationLifecycle
{
    public static ModerationReportState? Next(ModerationReportState current, ModerationActionType action) => (current, action) switch
    {
        (ModerationReportState.Pending, ModerationActionType.StartReview) => ModerationReportState.Reviewing,
        (ModerationReportState.Reviewing, ModerationActionType.Dismiss) => ModerationReportState.Dismissed,
        (ModerationReportState.Reviewing, ModerationActionType.RemoveContent or ModerationActionType.SuspendProfile) => ModerationReportState.ActionTaken,
        (ModerationReportState.Reviewing, ModerationActionType.RestoreToPending) => ModerationReportState.Pending,
        _ => null
    };
}
