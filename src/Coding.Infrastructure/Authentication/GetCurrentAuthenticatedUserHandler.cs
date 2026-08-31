using Coding.Application.Abstractions;
using Coding.Application.Features.Authentication;
using Coding.Data;
using Coding.DTOS.Auth;
using Coding.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Authentication;

public sealed class GetCurrentAuthenticatedUserHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetCurrentAuthenticatedUserQuery, AuthenticatedUser>
{
    public async Task<AuthenticatedUser> Handle(GetCurrentAuthenticatedUserQuery request, CancellationToken cancellationToken)
    {
        return await db.Users.AsNoTracking()
            .Where(user => user.ID == currentUser.UserId && !user.IsSuspended)
            .Select(user => new AuthenticatedUser(
                user.ID,
                user.FirstName,
                user.LastName,
                user.UserName,
                user.Email,
                user.EmailVerifiedAt.HasValue,
                user.UserRoles.Select(userRole => userRole.Role.Name).OrderBy(role => role).ToArray(),
                false,
                null,
                null))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedException("The authenticated account is unavailable.");
    }
}
