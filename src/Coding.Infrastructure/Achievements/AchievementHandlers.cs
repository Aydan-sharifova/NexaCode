using Coding.Application.Abstractions;
using Coding.Application.Features.Achievements;
using Coding.Data;
using Coding.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Coding.Domain.Services;

namespace Coding.Infrastructure.Achievements;

internal static class AchievementProfileMapper
{
    public static async Task<Guid> ResolveVisibleUser(AppDbContext db, string identifier, Guid viewerId, bool requireActivity, CancellationToken ct)
    {
        var normalized = identifier.Trim().TrimStart('@'); var publicId = normalized.ToUpperInvariant();
        var user = await db.Users.Include(x => x.DeveloperProfile).SingleOrDefaultAsync(x => !x.IsDeleted && !x.IsSuspended && (x.PublicId == publicId || EF.Functions.ILike(x.UserName, normalized)), ct) ?? throw new NotFoundException("User not found.");
        if (user.ID != viewerId && (user.DeveloperProfile?.IsProfilePublic == false || (requireActivity && user.DeveloperProfile?.IsActivityPublic == false))) throw new NotFoundException("User not found.");
        if (user.ID != viewerId && await db.UserBlocks.AnyAsync(x => (x.BlockerId == viewerId && x.BlockedId == user.ID) || (x.BlockerId == user.ID && x.BlockedId == viewerId), ct)) throw new NotFoundException("User not found.");
        return user.ID;
    }

    public static async Task<DeveloperAchievementProfile> Profile(AppDbContext db, Guid userId, CancellationToken ct)
    {
        var evidence = (await AchievementEvaluator.EvidenceAsync(db, userId, ct)).ToDictionary(x => x.Code);
        var awards = await db.UserAchievements.AsNoTracking().Where(x => x.UserId == userId).ToDictionaryAsync(x => x.AchievementId, ct);
        var catalog = await db.Achievements.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToListAsync(ct);
        var items = catalog.Select(x =>
        {
            awards.TryGetValue(x.ID, out var award); evidence.TryGetValue(x.Code, out var progress);
            return new AchievementItem(x.ID, x.Code, x.Title, x.Description, x.Icon, x.Category, x.Points, award is not null, award?.IsVerified ?? false, award?.UnlockedAt, award?.EvidenceType, award?.EvidenceId, progress?.Progress ?? 0, progress?.Target ?? 1);
        }).ToArray();
        var score = items.Where(x => x.Unlocked && x.Verified).Sum(x => x.Points);
        var level = AchievementPolicy.ContributionLevel(score);
        return new(userId, score, level, items.Count(x => x.Unlocked), items.Length, items);
    }
}

public sealed class GetMyAchievementsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetMyAchievementsQuery, DeveloperAchievementProfile>
{
    public Task<DeveloperAchievementProfile> Handle(GetMyAchievementsQuery request, CancellationToken ct) => AchievementProfileMapper.Profile(db, user.UserId, ct);
}

public sealed class GetUserAchievementsHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetUserAchievementsQuery, DeveloperAchievementProfile>
{
    public async Task<DeveloperAchievementProfile> Handle(GetUserAchievementsQuery request, CancellationToken ct)
    {
        var target = await AchievementProfileMapper.ResolveVisibleUser(db, request.PublicId, user.UserId, false, ct); return await AchievementProfileMapper.Profile(db, target, ct);
    }
}

public sealed class GetDeveloperJourneyHandler(AppDbContext db, ICurrentUser user) : IRequestHandler<GetDeveloperJourneyQuery, IReadOnlyList<DeveloperJourneyItem>>
{
    public async Task<IReadOnlyList<DeveloperJourneyItem>> Handle(GetDeveloperJourneyQuery request, CancellationToken ct)
    {
        var target = await AchievementProfileMapper.ResolveVisibleUser(db, request.PublicId, user.UserId, true, ct);
        return await db.UserAchievements.AsNoTracking().Where(x => x.UserId == target && x.IsVerified).OrderBy(x => x.UnlockedAt).Select(x => new DeveloperJourneyItem(x.Achievement.Code, x.Achievement.Title, x.Achievement.Description, x.UnlockedAt, x.EvidenceId)).ToListAsync(ct);
    }
}
