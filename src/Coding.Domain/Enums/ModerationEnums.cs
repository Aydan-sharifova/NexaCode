namespace Coding.Enums;

public enum ReportTargetType { Post, Comment, Project, Snippet, Profile }
public enum ModerationReportState { Pending, Reviewing, ActionTaken, Dismissed }
public enum ModerationActionType { StartReview, Dismiss, RemoveContent, SuspendProfile, RestoreToPending }
