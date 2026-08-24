using Coding.Application.Features.Users;
using Coding.Data;
using Coding.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Users;

public sealed class SocialAccessService(AppDbContext db) : ISocialAccessService
{
    public Task<bool> IsBlockedEitherWayAsync(Guid firstUserId, Guid secondUserId, CancellationToken ct) =>
        db.UserBlocks.AsNoTracking().AnyAsync(item =>
            item.BlockerId == firstUserId && item.BlockedId == secondUserId ||
            item.BlockerId == secondUserId && item.BlockedId == firstUserId, ct);

    public async Task EnsureCanInteractAsync(Guid actorUserId, Guid targetUserId, CancellationToken ct)
    {
        if (await IsBlockedEitherWayAsync(actorUserId, targetUserId, ct))
            throw new ForbiddenException("This interaction is unavailable because one of the users has blocked the other.");
    }
}
