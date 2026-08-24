using Coding.Enums;

namespace Coding.Domain.Services;

public static class ProjectLifecycle
{
    public static readonly TimeSpan DeadlineSoonWindow = TimeSpan.FromDays(7);

    public static ProjectStatus EffectiveStatus(ProjectStatus storedStatus, DateTime? deadlineAt, DateTime nowUtc)
    {
        if (storedStatus is ProjectStatus.Suspended or ProjectStatus.Archived or ProjectStatus.Deleted or ProjectStatus.Draft)
            return storedStatus;
        if (!deadlineAt.HasValue) return ProjectStatus.Active;
        if (deadlineAt.Value <= nowUtc) return ProjectStatus.DeadlineExpired;
        return deadlineAt.Value <= nowUtc.Add(DeadlineSoonWindow)
            ? ProjectStatus.DeadlineSoon
            : ProjectStatus.Active;
    }

    public static bool IsWorkspaceReadOnly(ProjectRole role, ProjectStatus status) =>
        role == ProjectRole.Viewer ||
        role == ProjectRole.Developer && status == ProjectStatus.DeadlineExpired ||
        status is ProjectStatus.Suspended or ProjectStatus.Archived or ProjectStatus.Deleted;
}
