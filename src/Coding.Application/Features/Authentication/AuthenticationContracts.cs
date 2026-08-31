using Coding.DTOS.Auth;
using MediatR;

namespace Coding.Application.Features.Authentication;

public sealed record GetCurrentAuthenticatedUserQuery : IRequest<AuthenticatedUser>;
