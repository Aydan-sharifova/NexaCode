using Coding.Domain.Services;
using Coding.Enums;

namespace Coding.Application.Features.Collaboration;

public static class CollaborationAccessPolicy
{
    public static bool CanWrite(ProjectRole role, ProjectStatus status, DateTime? deadlineAt, DateTime now) =>
        !ProjectLifecycle.IsWorkspaceReadOnly(role, ProjectLifecycle.EffectiveStatus(status, deadlineAt, now));
}
