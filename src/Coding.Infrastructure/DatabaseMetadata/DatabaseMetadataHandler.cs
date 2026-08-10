using Coding.Application.Abstractions;
using Coding.Application.Features.DatabaseMetadata;
using Coding.Data;
using Coding.Infrastructure.Projects;
using MediatR;

namespace Coding.Infrastructure.DatabaseMetadata;

public sealed class GetProjectDatabaseSchemaHandler(AppDbContext db, ICurrentUser user, IDatabaseMetadataProvider provider)
    : IRequestHandler<GetProjectDatabaseSchemaQuery, IReadOnlyList<DatabaseSchemaDto>>
{
    public async Task<IReadOnlyList<DatabaseSchemaDto>> Handle(GetProjectDatabaseSchemaQuery request, CancellationToken cancellationToken)
    {
        await ProjectAccess.RequireMemberAsync(db, request.ProjectId, user.UserId, cancellationToken);
        return await provider.GetSchemaAsync(cancellationToken);
    }
}
