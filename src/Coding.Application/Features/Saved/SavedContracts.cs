using MediatR;

namespace Coding.Application.Features.Saved;

public enum SavedContentType { All, Posts, Projects, Snippets, Templates, Agents }
public sealed record SavedAuthor(string PublicId,string UserName,string DisplayName,string? AvatarUrl);
public sealed record SavedPostItem(Guid Id,string Type,string Content,string? Language,SavedAuthor Author,DateTime SavedAt);
public sealed record SavedProjectItem(Guid Id,string Name,string? Description,string Language,string OwnerPublicId,DateTime SavedAt);
public sealed record SavedPackageItem(Guid Id,string Slug,string Title,string Description,string Category,IReadOnlyList<string> Tags,DateTime SavedAt);
public sealed record SavedContent(IReadOnlyList<SavedPostItem> Posts,IReadOnlyList<SavedProjectItem> Projects,IReadOnlyList<SavedPostItem> Snippets,IReadOnlyList<SavedPackageItem> Templates,IReadOnlyList<SavedPackageItem> Agents);
public sealed record GetSavedContentQuery(SavedContentType Type=SavedContentType.All,string? Search=null,int Limit=50):IRequest<SavedContent>;
public sealed record SetProjectSavedCommand(Guid ProjectId,bool Saved):IRequest<bool>;
