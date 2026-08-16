using Coding.Enums;
using FluentValidation;
using MediatR;
using System.Linq.Expressions;

namespace Coding.Application.Features.FileExplorer;

public sealed record WorkspaceNodeDto(Guid Id, Guid ProjectId, Guid? ParentId, string Name, WorkspaceNodeType NodeType, string Path, bool HasChildren, DateTime CreatedAt);
public sealed record FileContentDto(Guid NodeId, string Path, string Content, string ContentHash, string ConcurrencyToken, int VersionNumber, DateTime UpdatedAt);
public sealed record FileVersionDto(Guid Id, Guid NodeId, int VersionNumber, string ContentHash, Guid CreatedById, string CreatedBy, DateTime CreatedAt);
public sealed record FileVersionDetails(Guid Id, Guid NodeId, int VersionNumber, string Content, string ContentHash, Guid CreatedById, string CreatedBy, DateTime CreatedAt);
public sealed record VersionComparison(FileVersionDetails Left, FileVersionDetails Right, bool IsIdentical);

public sealed record CreateFolderCommand(Guid ProjectId, Guid? ParentId, string Name) : IRequest<WorkspaceNodeDto>;
public sealed record CreateFileCommand(Guid ProjectId, Guid? ParentId, string Name, string Content = "") : IRequest<WorkspaceNodeDto>;
public sealed record RenameNodeCommand(Guid NodeId, string Name) : IRequest<WorkspaceNodeDto>;
public sealed record DeleteNodeCommand(Guid NodeId) : IRequest;
public sealed record RestoreDeletedNodeCommand(Guid NodeId) : IRequest;
public sealed record MoveNodeCommand(Guid NodeId, Guid? ParentId) : IRequest<WorkspaceNodeDto>;
public sealed record SaveFileContentCommand(Guid NodeId, string Content, string ConcurrencyToken) : IRequest<FileContentDto>;
public sealed record RestoreFileVersionCommand(Guid NodeId, Guid VersionId) : IRequest<FileContentDto>;
public sealed record GetProjectFileTreeQuery(Guid ProjectId) : IRequest<IReadOnlyList<WorkspaceNodeDto>>;
public sealed record GetFolderChildrenQuery(Guid ProjectId, Guid? ParentId) : IRequest<IReadOnlyList<WorkspaceNodeDto>>;
public sealed record GetFileContentQuery(Guid NodeId) : IRequest<FileContentDto>;
public sealed record GetNodeDetailsQuery(Guid NodeId) : IRequest<WorkspaceNodeDto>;
public sealed record GetFileVersionsQuery(Guid NodeId) : IRequest<IReadOnlyList<FileVersionDto>>;
public sealed record GetFileVersionByIdQuery(Guid NodeId, Guid VersionId) : IRequest<FileVersionDetails>;
public sealed record CompareFileVersionsQuery(Guid NodeId, Guid LeftId, Guid RightId) : IRequest<VersionComparison>;

internal static class NodeNameRules
{
    private static readonly char[] Invalid = ['<', '>', ':', '"', '/', '\\', '|', '?', '*', '\0'];
    public static bool IsValid(string name) => !string.IsNullOrWhiteSpace(name) && name is not "." and not ".." && name.IndexOfAny(Invalid) < 0 && !name.EndsWith(' ') && !name.EndsWith('.');
}

public abstract class NodeNameValidator<T> : AbstractValidator<T>
{
    protected void ValidateName(Expression<Func<T, string>> selector) => RuleFor(selector).Must(NodeNameRules.IsValid).WithMessage("Name is empty or contains invalid filesystem characters.").MaximumLength(255);
}
public sealed class CreateFolderValidator : NodeNameValidator<CreateFolderCommand> { public CreateFolderValidator() { RuleFor(x => x.ProjectId).NotEmpty(); ValidateName(x => x.Name); } }
public sealed class CreateFileValidator : NodeNameValidator<CreateFileCommand> { public CreateFileValidator() { RuleFor(x => x.ProjectId).NotEmpty(); ValidateName(x => x.Name); RuleFor(x => x.Content).NotNull(); } }
public sealed class RenameNodeValidator : NodeNameValidator<RenameNodeCommand> { public RenameNodeValidator() { RuleFor(x => x.NodeId).NotEmpty(); ValidateName(x => x.Name); } }
public sealed class SaveFileContentValidator : AbstractValidator<SaveFileContentCommand> { public SaveFileContentValidator() { RuleFor(x => x.NodeId).NotEmpty(); RuleFor(x => x.Content).NotNull(); RuleFor(x => x.ConcurrencyToken).NotEmpty().Length(32); } }
