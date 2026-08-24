using System.Text.Json;
using Coding.Application.Abstractions;
using Coding.Application.Features.Saved;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Saved;

public sealed class GetSavedContentHandler(AppDbContext db,ICurrentUser user):IRequestHandler<GetSavedContentQuery,SavedContent>
{
    public async Task<SavedContent> Handle(GetSavedContentQuery request,CancellationToken ct)
    {
        var limit=Math.Clamp(request.Limit,1,100); var search=request.Search?.Trim();
        var postItems=new List<SavedPostItem>(); var snippetItems=new List<SavedPostItem>(); var projectItems=new List<SavedProjectItem>(); var templates=new List<SavedPackageItem>(); var agents=new List<SavedPackageItem>();
        var blocked=db.UserBlocks.Where(x=>x.BlockerId==user.UserId||x.BlockedId==user.UserId);
        if(request.Type is SavedContentType.All or SavedContentType.Posts or SavedContentType.Snippets)
        {
            var codeOnly=request.Type==SavedContentType.Snippets;
            var query=db.SavedSocialPosts.AsNoTracking().Where(x=>x.UserId==user.UserId&&!blocked.Any(b=>b.BlockerId==x.Post.AuthorId||b.BlockedId==x.Post.AuthorId)&&(codeOnly?x.Post.Type==PostType.Code:x.Post.Type!=PostType.Code));
            if(!string.IsNullOrWhiteSpace(search)) query=query.Where(x=>x.Post.Content.Contains(search)||x.Post.CodeLanguage!=null&&x.Post.CodeLanguage.Contains(search));
            var rows=await query.OrderByDescending(x=>x.CreatedAt).Take(limit).Select(x=>new{x.PostId,x.Post.Type,x.Post.Content,x.Post.CodeLanguage,x.Post.Author.PublicId,x.Post.Author.UserName,x.Post.Author.FirstName,x.Post.Author.LastName,x.Post.Author.AvatarUrl,SavedAt=x.CreatedAt}).ToListAsync(ct);
            var mapped=rows.Select(x=>new SavedPostItem(x.PostId,x.Type.ToString(),x.Content,x.CodeLanguage,new SavedAuthor(x.PublicId,x.UserName,(x.FirstName+" "+x.LastName).Trim(),x.AvatarUrl),x.SavedAt));
            if(codeOnly)snippetItems.AddRange(mapped);else postItems.AddRange(mapped);
            if(request.Type==SavedContentType.All)
            {
                var codes=db.SavedSocialPosts.AsNoTracking().Where(x=>x.UserId==user.UserId&&x.Post.Type==PostType.Code&&!blocked.Any(b=>b.BlockerId==x.Post.AuthorId||b.BlockedId==x.Post.AuthorId));
                if(!string.IsNullOrWhiteSpace(search))codes=codes.Where(x=>x.Post.Content.Contains(search)||x.Post.CodeLanguage!=null&&x.Post.CodeLanguage.Contains(search));
                var codeRows=await codes.OrderByDescending(x=>x.CreatedAt).Take(limit).Select(x=>new{x.PostId,x.Post.Type,x.Post.Content,x.Post.CodeLanguage,x.Post.Author.PublicId,x.Post.Author.UserName,x.Post.Author.FirstName,x.Post.Author.LastName,x.Post.Author.AvatarUrl,SavedAt=x.CreatedAt}).ToListAsync(ct);
                snippetItems.AddRange(codeRows.Select(x=>new SavedPostItem(x.PostId,x.Type.ToString(),x.Content,x.CodeLanguage,new SavedAuthor(x.PublicId,x.UserName,(x.FirstName+" "+x.LastName).Trim(),x.AvatarUrl),x.SavedAt)));
            }
        }
        if(request.Type is SavedContentType.All or SavedContentType.Projects)
        {
            var query=db.SavedProjects.AsNoTracking().Where(x=>x.UserId==user.UserId&&!blocked.Any(b=>b.BlockerId==x.Project.OwnerId||b.BlockedId==x.Project.OwnerId));
            if(!string.IsNullOrWhiteSpace(search))query=query.Where(x=>x.Project.Name.Contains(search)||x.Project.Description!=null&&x.Project.Description.Contains(search));
            var rows=await query.OrderByDescending(x=>x.CreatedAt).Take(limit).Select(x=>new{x.ProjectId,x.Project.Name,x.Project.Description,x.Project.DefaultLanguage,x.Project.Owner.PublicId,SavedAt=x.CreatedAt}).ToListAsync(ct);
            projectItems.AddRange(rows.Select(x=>new SavedProjectItem(x.ProjectId,x.Name,x.Description,x.DefaultLanguage,x.PublicId,x.SavedAt)));
        }
        async Task<List<SavedPackageItem>> Load(MarketplaceCategory category)
        {
            var query=db.SavedMarketplaceItems.AsNoTracking().Where(x=>x.UserId==user.UserId&&x.MarketplaceItem.Category==category&&x.MarketplaceItem.Status==MarketplaceItemStatus.Published&&!blocked.Any(b=>b.BlockerId==x.MarketplaceItem.AuthorId||b.BlockedId==x.MarketplaceItem.AuthorId));
            if(!string.IsNullOrWhiteSpace(search))query=query.Where(x=>x.MarketplaceItem.Title.Contains(search)||x.MarketplaceItem.Description.Contains(search));
            var rows=await query.OrderByDescending(x=>x.CreatedAt).Take(limit).Select(x=>new{x.MarketplaceItemId,x.MarketplaceItem.Slug,x.MarketplaceItem.Title,x.MarketplaceItem.Description,x.MarketplaceItem.Category,x.MarketplaceItem.TagsJson,SavedAt=x.CreatedAt}).ToListAsync(ct);
            return rows.Select(x=>new SavedPackageItem(x.MarketplaceItemId,x.Slug,x.Title,x.Description,x.Category.ToString(),JsonSerializer.Deserialize<string[]>(x.TagsJson)??[],x.SavedAt)).ToList();
        }
        if(request.Type is SavedContentType.All or SavedContentType.Templates)templates=await Load(MarketplaceCategory.ProjectTemplate);
        if(request.Type is SavedContentType.All or SavedContentType.Agents)agents=await Load(MarketplaceCategory.AiAgent);
        return new(postItems,projectItems,snippetItems,templates,agents);
    }
}

public sealed class SetProjectSavedHandler(AppDbContext db,ICurrentUser user):IRequestHandler<SetProjectSavedCommand,bool>
{
    public async Task<bool> Handle(SetProjectSavedCommand request,CancellationToken ct)
    {
        var project=await db.Projects.AsNoTracking().SingleOrDefaultAsync(x=>x.ID==request.ProjectId&&x.IsPublic,ct)??throw new NotFoundException("Public project not found.");
        if(await db.UserBlocks.AnyAsync(x=>x.BlockerId==user.UserId&&x.BlockedId==project.OwnerId||x.BlockerId==project.OwnerId&&x.BlockedId==user.UserId,ct))throw new ForbiddenException("Blocked profiles cannot save this project.");
        var existing=await db.SavedProjects.SingleOrDefaultAsync(x=>x.ProjectId==request.ProjectId&&x.UserId==user.UserId,ct);
        if(request.Saved&&existing is null)db.SavedProjects.Add(new SavedProject{ProjectId=request.ProjectId,UserId=user.UserId}); else if(!request.Saved&&existing is not null)db.SavedProjects.Remove(existing);
        await db.SaveChangesAsync(ct); return request.Saved;
    }
}
