using Coding.Application.Abstractions;
using Coding.Application.Features.Activities;
using Coding.Application.Features.Notifications;
using Coding.Application.Features.Users;
using Coding.Data;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Models;
using Coding.Infrastructure.Notifications;
using Coding.Application.Features.Achievements;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Users;

public sealed class SearchUsersHandler(IUserLookupService users, ICurrentUser currentUser) : IRequestHandler<SearchUsersQuery, UserSearchPage>
{
    public Task<UserSearchPage> Handle(SearchUsersQuery request, CancellationToken ct) =>
        users.SearchAsync(currentUser.UserId, request.Query, request.Page, request.PageSize, ct);
}

internal static class PublicUserResolver
{
    public static IQueryable<User> ByIdentifier(this IQueryable<User> query, string rawIdentifier)
    {
        var identifier = rawIdentifier.Trim().TrimStart('@');
        var publicId = identifier.ToUpperInvariant();
        var hasUserId = Guid.TryParse(identifier, out var userId);
        return query.Where(user => !user.IsDeleted && !user.IsSuspended &&
            (user.PublicId == publicId || EF.Functions.ILike(user.UserName, identifier) || (hasUserId && user.ID == userId)));
    }

    public static PublicUserProfileDto ToProfile(User user, Guid viewerId, int followers, int following, bool isFollowing, bool isBlockedByMe)
    {
        var profile = user.DeveloperProfile;
        return new PublicUserProfileDto(
            user.ID,
            user.PublicId,
            string.IsNullOrWhiteSpace(profile?.DisplayName) ? $"{user.FirstName} {user.LastName}".Trim() : profile.DisplayName,
            user.UserName,
            user.AvatarUrl,
            profile?.CoverImageUrl,
            profile?.Bio ?? user.Bio,
            profile?.Headline,
            profile?.Location,
            profile?.WebsiteUrl,
            profile?.GitHubUrl,
            profile?.LinkedInUrl,
            profile?.PortfolioUrl,
            profile?.PrimaryRole,
            profile?.ExperienceLevel,
            profile?.Skills ?? [],
            profile?.LearningTopics ?? [],
            user.CreatedAt,
            user.OwnedProjects.Count(project => project.IsPublic),
            user.ID == viewerId || profile?.AreFollowersPublic != false ? followers : null,
            user.ID == viewerId || profile?.AreFollowersPublic != false ? following : null,
            isFollowing,
            isBlockedByMe,
            user.ID == viewerId,
            profile?.IsProfilePublic ?? true,
            profile?.IsActivityPublic ?? true,
            profile?.AreFollowersPublic ?? true);
    }
}

public sealed class GetPublicUserProfileHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetPublicUserProfileQuery, PublicUserProfileDto>
{
    public async Task<PublicUserProfileDto> Handle(GetPublicUserProfileQuery request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(item => item.DeveloperProfile).Include(item => item.OwnedProjects)
            .ByIdentifier(request.PublicId).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("User not found.");
        if (user.ID != currentUser.UserId && user.DeveloperProfile is { IsProfilePublic: false })
            throw new NotFoundException("User not found.");
        var blockedByTarget = user.ID != currentUser.UserId && await db.UserBlocks.AnyAsync(item => item.BlockerId == user.ID && item.BlockedId == currentUser.UserId, ct);
        if (blockedByTarget) throw new NotFoundException("User not found.");
        var isBlockedByMe = user.ID != currentUser.UserId && await db.UserBlocks.AnyAsync(item => item.BlockerId == currentUser.UserId && item.BlockedId == user.ID, ct);
        var followers = await db.UserFollows.CountAsync(item => item.FollowingId == user.ID, ct);
        var following = await db.UserFollows.CountAsync(item => item.FollowerId == user.ID, ct);
        var isFollowing = user.ID != currentUser.UserId && await db.UserFollows.AnyAsync(item => item.FollowerId == currentUser.UserId && item.FollowingId == user.ID, ct);
        return PublicUserResolver.ToProfile(user, currentUser.UserId, followers, following, isFollowing, isBlockedByMe);
    }
}

public sealed class GetDeveloperPortfolioHandler(AppDbContext db,ICurrentUser currentUser):IRequestHandler<GetDeveloperPortfolioQuery,DeveloperPortfolioDto>
{
    public async Task<DeveloperPortfolioDto> Handle(GetDeveloperPortfolioQuery request,CancellationToken ct)
    {
        var target=await db.Users.AsNoTracking().Include(x=>x.DeveloperProfile).ByIdentifier(request.PublicId)
            .Select(x=>new{x.ID,x.DeveloperProfile,x.PublicId}).SingleOrDefaultAsync(ct)??throw new NotFoundException("User not found.");
        if(target.ID!=currentUser.UserId&&target.DeveloperProfile?.IsProfilePublic==false)throw new NotFoundException("User not found.");
        if(target.ID!=currentUser.UserId&&await db.UserBlocks.AnyAsync(x=>x.BlockerId==currentUser.UserId&&x.BlockedId==target.ID||x.BlockerId==target.ID&&x.BlockedId==currentUser.UserId,ct))throw new NotFoundException("User not found.");
        var activityVisible=target.ID==currentUser.UserId||target.DeveloperProfile?.IsActivityPublic!=false;
        var followersVisible=target.ID==currentUser.UserId||target.DeveloperProfile?.AreFollowersPublic!=false;
        var people=followersVisible?await db.Users.AsNoTracking().Where(x=>!x.IsDeleted&&!x.IsSuspended&&
                !db.UserBlocks.Any(b=>b.BlockerId==currentUser.UserId&&b.BlockedId==x.ID||b.BlockerId==x.ID&&b.BlockedId==currentUser.UserId))
            .Select(x=>new{x.ID,x.PublicId,x.UserName,x.FirstName,x.LastName,x.AvatarUrl,Display=x.DeveloperProfile==null?null:x.DeveloperProfile.DisplayName}).ToListAsync(ct):[];
        var followerIds=followersVisible?await db.UserFollows.AsNoTracking().Where(x=>x.FollowingId==target.ID).OrderByDescending(x=>x.CreatedAt).Take(30).Select(x=>x.FollowerId).ToListAsync(ct):[];
        var followingIds=followersVisible?await db.UserFollows.AsNoTracking().Where(x=>x.FollowerId==target.ID).OrderByDescending(x=>x.CreatedAt).Take(30).Select(x=>x.FollowingId).ToListAsync(ct):[];
        PortfolioPersonDto Person(Guid id){var x=people.Single(p=>p.ID==id);return new(x.PublicId,x.UserName,string.IsNullOrWhiteSpace(x.Display)?($"{x.FirstName} {x.LastName}".Trim()):x.Display,x.AvatarUrl);}
        var followers=followerIds.Where(id=>people.Any(x=>x.ID==id)).Select(Person).ToList();var following=followingIds.Where(id=>people.Any(x=>x.ID==id)).Select(Person).ToList();
        if(!activityVisible)return new(false,[],[],[],null,followers,following);
        var posts=await db.SocialPosts.AsNoTracking().Where(x=>x.AuthorId==target.ID&&(x.ProjectId==null||x.Project!.IsPublic))
            .OrderByDescending(x=>x.CreatedAt).Take(30).Select(x=>new PortfolioPostDto(x.ID,x.Type.ToString(),x.Content,x.CodeLanguage,x.ImageUrl,x.ProjectId,x.Project==null?null:x.Project.Name,
                x.Reactions.Count,x.Comments.Count,x.Saves.Count,x.Shares.Count,x.CreatedAt)).ToListAsync(ct);
        var snippets=posts.Where(x=>x.Type==PostType.Code.ToString()).Take(8).ToList();
        var publicProjects=await db.Projects.AsNoTracking().Where(x=>x.OwnerId==target.ID&&x.IsPublic).Select(x=>new{x.ID,x.Name,x.CreatedAt}).ToListAsync(ct);
        var commits=await db.GitCommits.AsNoTracking().Where(x=>x.UserId==target.ID&&x.Project.IsPublic).OrderByDescending(x=>x.CommitDate).Take(20).Select(x=>new PortfolioActivityDto("Commit","Commit",x.CommitMessage,x.CommitDate,x.ID)).ToListAsync(ct);
        var merges=await db.PullRequests.AsNoTracking().Where(x=>x.AuthorId==target.ID&&x.Status==PullRequestStatus.Merged&&x.Project.IsPublic).OrderByDescending(x=>x.MergedAt).Take(20)
            .Select(x=>new PortfolioActivityDto("Merge","Pull request merged",x.Title,x.MergedAt??x.UpdatedAt,x.ID)).ToListAsync(ct);
        var awards=await db.UserAchievements.AsNoTracking().Where(x=>x.UserId==target.ID&&x.IsVerified).OrderByDescending(x=>x.UnlockedAt).Take(20)
            .Select(x=>new PortfolioActivityDto("Achievement",x.Achievement.Title,x.Achievement.Description,x.UnlockedAt,x.EvidenceId)).ToListAsync(ct);
        var activity=publicProjects.Select(x=>new PortfolioActivityDto("Project","Published project",x.Name,x.CreatedAt,x.ID)).Concat(commits).Concat(merges).Concat(awards).Concat(posts.Take(10).Select(x=>new PortfolioActivityDto("Post",x.Type+" post",x.Content.Length<=160?x.Content:x.Content[..160]+"…",x.CreatedAt,x.Id))).OrderByDescending(x=>x.OccurredAt).Take(30).ToList();
        var accepted=await db.PullRequestReviews.CountAsync(x=>x.ReviewerId==target.ID&&x.Decision==PullRequestReviewDecision.Approved&&x.PullRequest.Project.IsPublic,ct);
        var deployments=await db.ActivityLogs.CountAsync(x=>x.UserId==target.ID&&x.ActionType=="DeploymentSucceeded"&&(x.ProjectId==null||x.Project!.IsPublic),ct);
        var contribution=new ContributionSummaryDto(await db.GitCommits.CountAsync(x=>x.UserId==target.ID&&x.Project.IsPublic,ct),
            await db.PullRequests.CountAsync(x=>x.AuthorId==target.ID&&x.Status==PullRequestStatus.Merged&&x.Project.IsPublic,ct),accepted,publicProjects.Count,
            await db.SocialPosts.CountAsync(x=>x.AuthorId==target.ID&&x.Type==PostType.Code&&x.Saves.Any(),ct),deployments,
            await db.SocialPosts.CountAsync(x=>x.AuthorId==target.ID&&(x.ProjectId==null||x.Project!.IsPublic),ct)+
            await db.SocialPostComments.CountAsync(x=>x.AuthorId==target.ID&&(x.Post.ProjectId==null||x.Post.Project!.IsPublic),ct),awards.Count);
        return new(true,posts.Take(12).ToList(),snippets,activity,contribution,followers,following);
    }
}

public sealed class UpdateDeveloperProfileHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<UpdateDeveloperProfileCommand, PublicUserProfileDto>
{
    public async Task<PublicUserProfileDto> Handle(UpdateDeveloperProfileCommand request, CancellationToken ct)
    {
        var user = await db.Users.Include(item => item.DeveloperProfile).Include(item => item.OwnedProjects)
            .SingleAsync(item => item.ID == currentUser.UserId, ct);
        var now = DateTime.UtcNow;
        var profile = user.DeveloperProfile;
        if (profile is null)
        {
            profile = new DeveloperProfile { ID = Guid.NewGuid(), UserId = user.ID, CreatedAt = now, CreatAt = now };
            db.DeveloperProfiles.Add(profile);
            user.DeveloperProfile = profile;
        }
        profile.DisplayName = request.DisplayName.Trim();
        profile.Bio = Clean(request.Bio);
        profile.Headline = Clean(request.Headline);
        profile.Location = Clean(request.Location);
        profile.WebsiteUrl = Clean(request.WebsiteUrl);
        profile.GitHubUrl = Clean(request.GitHubUrl);
        profile.LinkedInUrl = Clean(request.LinkedInUrl);
        profile.PortfolioUrl = Clean(request.PortfolioUrl);
        profile.PrimaryRole = Clean(request.PrimaryRole);
        profile.ExperienceLevel = Clean(request.ExperienceLevel);
        profile.Skills = NormalizeTags(request.Skills, 30);
        profile.LearningTopics = NormalizeTags(request.LearningTopics, 20);
        profile.IsProfilePublic = request.IsProfilePublic;
        profile.IsActivityPublic = request.IsActivityPublic;
        profile.AreFollowersPublic = request.AreFollowersPublic;
        profile.UpdatedAt = now;
        profile.UpdateAt = now;
        user.Bio = profile.Bio;
        user.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        var followers = await db.UserFollows.CountAsync(item => item.FollowingId == user.ID, ct);
        var following = await db.UserFollows.CountAsync(item => item.FollowerId == user.ID, ct);
        return PublicUserResolver.ToProfile(user, currentUser.UserId, followers, following, false, false);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string[] NormalizeTags(IReadOnlyList<string>? values, int maximum) => (values ?? [])
        .Select(value => value.Trim()).Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).Take(maximum).ToArray();
}

public sealed class FollowUserHandler(AppDbContext db, ICurrentUser currentUser, INotificationService notifications, ISocialAccessService socialAccess, IAchievementEvaluator achievements) : IRequestHandler<FollowUserCommand, FollowStateDto>
{
    public async Task<FollowStateDto> Handle(FollowUserCommand request, CancellationToken ct)
    {
        var target = await db.Users.AsNoTracking().Include(item => item.DeveloperProfile).ByIdentifier(request.PublicId)
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("User not found.");
        if (target.ID == currentUser.UserId) throw new ConflictException("You cannot follow yourself.");
        await socialAccess.EnsureCanInteractAsync(currentUser.UserId, target.ID, ct);
        if (target.DeveloperProfile is { IsProfilePublic: false }) throw new NotFoundException("User not found.");
        var exists = await db.UserFollows.AnyAsync(item => item.FollowerId == currentUser.UserId && item.FollowingId == target.ID, ct);
        if (!exists)
        {
            var now = DateTime.UtcNow;
            db.UserFollows.Add(new UserFollow { ID = Guid.NewGuid(), FollowerId = currentUser.UserId, FollowingId = target.ID, CreatedAt = now, CreatAt = now });
            await db.SaveChangesAsync(ct);
            var actor = await db.Users.AsNoTracking().Where(item => item.ID == currentUser.UserId)
                .Select(item => item.UserName).SingleAsync(ct);
            await notifications.CreateAsync(new CreateNotificationRequest(target.ID, NotificationType.Follow,
                "New follower", $"@{actor} started following you.", currentUser.UserId, nameof(User)), ct);
            await achievements.EvaluateAsync(target.ID, ct);
        }
        int? count = target.DeveloperProfile?.AreFollowersPublic != false
            ? await db.UserFollows.CountAsync(item => item.FollowingId == target.ID, ct)
            : null;
        return new(true, count);
    }
}

public sealed class UnfollowUserHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<UnfollowUserCommand, FollowStateDto>
{
    public async Task<FollowStateDto> Handle(UnfollowUserCommand request, CancellationToken ct)
    {
        var targetId = await db.Users.AsNoTracking().ByIdentifier(request.PublicId).Select(item => (Guid?)item.ID)
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("User not found.");
        await db.UserFollows.Where(item => item.FollowerId == currentUser.UserId && item.FollowingId == targetId)
            .ExecuteDeleteAsync(ct);
        var followersArePublic = await db.DeveloperProfiles.AsNoTracking().Where(item => item.UserId == targetId)
            .Select(item => (bool?)item.AreFollowersPublic).SingleOrDefaultAsync(ct) ?? true;
        int? count = followersArePublic ? await db.UserFollows.CountAsync(item => item.FollowingId == targetId, ct) : null;
        return new(false, count);
    }
}

public sealed class BlockUserHandler(AppDbContext db, ICurrentUser currentUser, IActivityLogger activity) : IRequestHandler<BlockUserCommand, BlockStateDto>
{
    public async Task<BlockStateDto> Handle(BlockUserCommand request, CancellationToken ct)
    {
        var targetId = await db.Users.AsNoTracking().ByIdentifier(request.PublicId).Select(item => (Guid?)item.ID)
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("User not found.");
        if (targetId == currentUser.UserId) throw new ConflictException("You cannot block yourself.");
        var exists = await db.UserBlocks.AnyAsync(item => item.BlockerId == currentUser.UserId && item.BlockedId == targetId, ct);
        if (!exists)
        {
            var now = DateTime.UtcNow;
            db.UserBlocks.Add(new UserBlock { ID = Guid.NewGuid(), BlockerId = currentUser.UserId, BlockedId = targetId, CreatedAt = now, CreatAt = now });
            var follows = await db.UserFollows.Where(item =>
                item.FollowerId == currentUser.UserId && item.FollowingId == targetId ||
                item.FollowerId == targetId && item.FollowingId == currentUser.UserId).ToListAsync(ct);
            db.UserFollows.RemoveRange(follows);
            await db.SaveChangesAsync(ct);
            await activity.LogAsync(new(currentUser.UserId, null, "UserBlocked", nameof(UserBlock), targetId, "Blocked a developer account."), ct);
        }
        return new(true);
    }
}

public sealed class UnblockUserHandler(AppDbContext db, ICurrentUser currentUser, IActivityLogger activity) : IRequestHandler<UnblockUserCommand, BlockStateDto>
{
    public async Task<BlockStateDto> Handle(UnblockUserCommand request, CancellationToken ct)
    {
        var identifier = request.PublicId.Trim().TrimStart('@');
        var targetId = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(item => !item.IsDeleted && (item.PublicId == identifier.ToUpper() || EF.Functions.ILike(item.UserName, identifier)))
            .Select(item => (Guid?)item.ID).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("User not found.");
        var removed = await db.UserBlocks.Where(item => item.BlockerId == currentUser.UserId && item.BlockedId == targetId).ExecuteDeleteAsync(ct);
        if (removed > 0) await activity.LogAsync(new(currentUser.UserId, null, "UserUnblocked", nameof(UserBlock), targetId, "Unblocked a developer account."), ct);
        return new(false);
    }
}

public sealed class GetBlockedUsersHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetBlockedUsersQuery, BlockedUserPage>
{
    public async Task<BlockedUserPage> Handle(GetBlockedUsersQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);
        var cursor = NotificationCursor.Decode(request.Cursor);
        var query = db.UserBlocks.AsNoTracking().Where(item => item.BlockerId == currentUser.UserId);
        if (cursor.HasValue) query = query.Where(item => item.CreatedAt < cursor.Value.CreatedAt || item.CreatedAt == cursor.Value.CreatedAt && item.ID.CompareTo(cursor.Value.Id) < 0);
        var rows = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.ID).Select(item => new
        {
            item.ID, item.CreatedAt, item.Blocked.PublicId, item.Blocked.UserName, item.Blocked.FirstName, item.Blocked.LastName, item.Blocked.AvatarUrl,
            DisplayName = item.Blocked.DeveloperProfile == null || item.Blocked.DeveloperProfile.DisplayName == "" ? (item.Blocked.FirstName + " " + item.Blocked.LastName).Trim() : item.Blocked.DeveloperProfile.DisplayName
        }).Take(limit + 1).ToListAsync(ct);
        var hasMore = rows.Count > limit; if (hasMore) rows.RemoveAt(rows.Count - 1); var last = rows.LastOrDefault();
        return new(rows.Select(item => new BlockedUserDto(item.PublicId, item.DisplayName, item.UserName, item.AvatarUrl, item.CreatedAt)).ToArray(),
            hasMore && last is not null ? NotificationCursor.Encode(last.CreatedAt, last.ID) : null);
    }
}

public sealed class GetPublicUserProjectsHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetPublicUserProjectsQuery, PublicProjectPage>
{
    public async Task<PublicProjectPage> Handle(GetPublicUserProjectsQuery request, CancellationToken ct)
    {
        var owner = await db.Users.AsNoTracking().ByIdentifier(request.PublicId)
            .Select(user => new { user.ID, IsPublic = user.DeveloperProfile == null || user.DeveloperProfile.IsProfilePublic })
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("User not found.");
        if (owner.ID != currentUser.UserId && !owner.IsPublic) throw new NotFoundException("User not found.");
        if (owner.ID != currentUser.UserId && await db.UserBlocks.AnyAsync(item =>
            item.BlockerId == currentUser.UserId && item.BlockedId == owner.ID || item.BlockerId == owner.ID && item.BlockedId == currentUser.UserId, ct))
            throw new NotFoundException("User not found.");
        var ownerId = owner.ID;
        var rows = await db.Projects.AsNoTracking()
            .Where(project => project.IsPublic && project.OwnerId == ownerId)
            .OrderByDescending(project => project.UpdateAt ?? project.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize + 1)
            .Select(project => new PublicProjectDto(project.ID, project.Name, project.Description, project.DefaultLanguage,
                project.UpdateAt ?? project.CreatedAt)).ToListAsync(ct);
        return new(rows.Take(request.PageSize).ToList(), request.Page, request.PageSize, rows.Count > request.PageSize);
    }
}

public sealed class GetPublicProjectDetailsHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetPublicProjectDetailsQuery, PublicProjectDetailsDto>
{
    public async Task<PublicProjectDetailsDto> Handle(GetPublicProjectDetailsQuery request, CancellationToken ct)
    {
        var result=await db.Projects.AsNoTracking().Where(project => project.ID == request.ProjectId && project.IsPublic &&
            !db.UserBlocks.Any(block => block.BlockerId == currentUser.UserId && block.BlockedId == project.OwnerId || block.BlockerId == project.OwnerId && block.BlockedId == currentUser.UserId))
            .Select(project => new PublicProjectDetailsDto(project.ID, project.Name, project.Description, project.DefaultLanguage,
                project.Owner.PublicId, (project.Owner.FirstName + " " + project.Owner.LastName).Trim(), project.CreatedAt,
                project.UpdateAt ?? project.CreatedAt))
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Public project not found.");
        var today=DateOnly.FromDateTime(DateTime.UtcNow);
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO ""ProjectViews"" (""ProjectId"",""UserId"",""ViewedOn"",""ViewedAt"") VALUES ({request.ProjectId},{currentUser.UserId},{today},{DateTime.UtcNow}) ON CONFLICT (""ProjectId"",""UserId"",""ViewedOn"") DO UPDATE SET ""ViewedAt""=EXCLUDED.""ViewedAt""",ct);
        return result;
    }
}

public sealed class GetPublicProjectTreeHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetPublicProjectTreeQuery, IReadOnlyList<PublicProjectNodeDto>>
{
    public async Task<IReadOnlyList<PublicProjectNodeDto>> Handle(GetPublicProjectTreeQuery request, CancellationToken ct)
    {
        if (!await db.Projects.AsNoTracking().AnyAsync(project => project.ID == request.ProjectId && project.IsPublic &&
            !db.UserBlocks.Any(block => block.BlockerId == currentUser.UserId && block.BlockedId == project.OwnerId || block.BlockerId == project.OwnerId && block.BlockedId == currentUser.UserId), ct))
            throw new NotFoundException("Public project not found.");
        return await db.WorkspaceNodes.AsNoTracking().Where(node => node.ProjectId == request.ProjectId)
            .OrderBy(node => node.NodeType).ThenBy(node => node.Name)
            .Select(node => new PublicProjectNodeDto(node.ID, node.ParentId, node.Name,
                node.NodeType == Coding.Enums.WorkspaceNodeType.Folder ? "Folder" : "File", node.Name,
                db.WorkspaceNodes.Any(child => child.ParentId == node.ID)))
            .ToListAsync(ct);
    }
}

public sealed class GetPublicProjectFileHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetPublicProjectFileQuery, PublicProjectFileDto>
{
    public async Task<PublicProjectFileDto> Handle(GetPublicProjectFileQuery request, CancellationToken ct) =>
        await db.FileContents.AsNoTracking()
            .Where(file => file.NodeId == request.NodeId && file.Node.ProjectId == request.ProjectId && file.Node.Project.IsPublic &&
                !db.UserBlocks.Any(block => block.BlockerId == currentUser.UserId && block.BlockedId == file.Node.Project.OwnerId || block.BlockerId == file.Node.Project.OwnerId && block.BlockedId == currentUser.UserId))
            .Select(file => new PublicProjectFileDto(file.NodeId, file.Node.Name, file.Content, file.VersionNumber, file.UpdatedAt))
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Public project file not found.");
}
