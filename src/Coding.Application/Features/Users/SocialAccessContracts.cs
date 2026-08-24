namespace Coding.Application.Features.Users;

public interface ISocialAccessService
{
    Task<bool> IsBlockedEitherWayAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken);
    Task EnsureCanInteractAsync(Guid actorUserId, Guid targetUserId, CancellationToken cancellationToken);
}
