using System.Text;
using Coding.Application.Abstractions;
using Coding.Application.Features.Notifications;
using Coding.Data;
using Coding.Exceptions;
using Coding.Enums;
using Coding.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Coding.Infrastructure.Notifications;

internal static class NotificationCursor
{
    public static string Encode(DateTime createdAt, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt.Ticks}:{id:N}"));

    public static (DateTime CreatedAt, Guid Id)? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParseExact(parts[1], "N", out var id)) throw new FormatException();
            return (new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch (Exception error) when (error is FormatException or ArgumentException)
        {
            throw new FluentValidation.ValidationException("The pagination cursor is invalid.");
        }
    }
}

public sealed class NotificationService(AppDbContext db, INotificationRealtimePublisher realtime) : INotificationService
{
    public async Task<NotificationItem?> CreateAsync(CreateNotificationRequest request, CancellationToken ct = default)
    {
        var enabled = await db.UserNotificationPreferences.AsNoTracking()
            .Where(item => item.UserId == request.UserId && item.Type == request.Type)
            .Select(item => (bool?)item.InAppEnabled).SingleOrDefaultAsync(ct) ?? true;
        if (!enabled) return null;
        var key=NotificationDeduplication.Key(request,DateTime.UtcNow);
        var existing=await db.Notifications.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==request.UserId&&x.DeduplicationKey==key,ct);
        if(existing is not null)return ToItem(existing);
        var entity = ToEntity(request,key);
        db.Notifications.Add(entity);
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){db.Entry(entity).State=EntityState.Detached;existing=await db.Notifications.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==request.UserId&&x.DeduplicationKey==key,ct);if(existing is not null)return ToItem(existing);throw;}
        var item = ToItem(entity);
        await realtime.NotificationReceivedAsync(item, ct);
        await realtime.UnreadCountUpdatedAsync(request.UserId, await UnreadCount(request.UserId, ct), ct);
        return item;
    }

    public async Task<IReadOnlyList<NotificationItem>> CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct = default)
    {
        var distinct = requests.DistinctBy(item => (item.UserId, item.Type, item.RelatedEntityId)).ToArray();
        if (distinct.Length == 0) return [];
        var disabled = await db.UserNotificationPreferences.AsNoTracking().Where(item => !item.InAppEnabled)
            .Select(item => new { item.UserId, item.Type }).ToListAsync(ct);
        var now=DateTime.UtcNow;
        var candidates=distinct.Where(request => !disabled.Any(item => item.UserId == request.UserId && item.Type == request.Type)).Select(request=>(Request:request,Key:NotificationDeduplication.Key(request,now))).DistinctBy(x=>x.Key).ToArray();
        var keys=candidates.Select(x=>x.Key).ToArray(); var existingKeys=(await db.Notifications.AsNoTracking().Where(x=>keys.Contains(x.DeduplicationKey!)).Select(x=>x.DeduplicationKey!).ToListAsync(ct)).ToHashSet();
        var entities = candidates.Where(x=>!existingKeys.Contains(x.Key)).Select(x=>ToEntity(x.Request,x.Key)).ToArray();
        if(entities.Length==0)return [];
        db.Notifications.AddRange(entities);
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){foreach(var entity in entities)db.Entry(entity).State=EntityState.Detached;var raced=await db.Notifications.AsNoTracking().Where(x=>keys.Contains(x.DeduplicationKey!)).ToListAsync(ct);if(raced.Count>0)return raced.Select(ToItem).ToArray();throw;}
        var items = entities.Select(ToItem).ToArray();
        foreach (var item in items) await realtime.NotificationReceivedAsync(item, ct);
        foreach (var userId in entities.Select(item => item.UserId).Distinct()) await realtime.UnreadCountUpdatedAsync(userId, await UnreadCount(userId, ct), ct);
        return items;
    }

    public async Task MarkRelatedReadAsync(Guid userId, NotificationType type, Guid relatedEntityId, CancellationToken ct = default)
    {
        var ids = await db.Notifications
            .Where(item => item.UserId == userId && item.Type == type && item.RelatedEntityId == relatedEntityId && !item.IsRead)
            .Select(item => item.ID)
            .ToListAsync(ct);
        if (ids.Count == 0) return;
        var now = DateTime.UtcNow;
        await db.Notifications.Where(item => ids.Contains(item.ID)).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.IsRead, true)
            .SetProperty(item => item.ReadAt, now)
            .SetProperty(item => item.UpdateAt, now), ct);
        foreach (var id in ids) await realtime.NotificationReadAsync(userId, id, ct);
        await realtime.UnreadCountUpdatedAsync(userId, await UnreadCount(userId, ct), ct);
    }

    private Task<int> UnreadCount(Guid userId, CancellationToken ct) => db.Notifications.CountAsync(item => item.UserId == userId && !item.IsRead, ct);
    private static Notification ToEntity(CreateNotificationRequest request,string key) => new() { ID = Guid.NewGuid(), UserId = request.UserId, Type = request.Type, Title = request.Title, Message = request.Message, RelatedEntityId = request.RelatedEntityId, RelatedEntityType = request.RelatedEntityType, DeduplicationKey=key, IsRead = false, CreatedAt = DateTime.UtcNow, CreatAt = DateTime.UtcNow };
    private static NotificationItem ToItem(Notification item) => new(item.ID, item.UserId, item.Type, item.Title, item.Message, item.RelatedEntityId, item.RelatedEntityType, item.IsRead, item.CreatedAt, item.ReadAt);
}

public sealed class GetNotificationsHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetNotificationsQuery, NotificationPage>
{
    public async Task<NotificationPage> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);
        var cursor = NotificationCursor.Decode(request.Cursor);
        var query = db.Notifications.AsNoTracking().Where(item => item.UserId == currentUser.UserId);
        if (request.IsRead.HasValue) query = query.Where(item => item.IsRead == request.IsRead);
        if (cursor.HasValue) query = query.Where(item => item.CreatedAt < cursor.Value.CreatedAt || item.CreatedAt == cursor.Value.CreatedAt && item.ID.CompareTo(cursor.Value.Id) < 0);
        var entities = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.ID).Take(limit + 1).ToListAsync(ct);
        var hasMore = entities.Count > limit;
        if (hasMore) entities.RemoveAt(entities.Count - 1);
        var items = entities.Select(item => new NotificationItem(item.ID, item.UserId, item.Type, item.Title, item.Message, item.RelatedEntityId, item.RelatedEntityType, item.IsRead, item.CreatedAt, item.ReadAt)).ToArray();
        var last = entities.LastOrDefault();
        var unread = await db.Notifications.CountAsync(item => item.UserId == currentUser.UserId && !item.IsRead, ct);
        return new NotificationPage(items, hasMore && last is not null ? NotificationCursor.Encode(last.CreatedAt, last.ID) : null, unread);
    }
}

public sealed class MarkNotificationReadHandler(AppDbContext db, ICurrentUser currentUser, INotificationRealtimePublisher realtime) : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var item = await db.Notifications.SingleOrDefaultAsync(notification => notification.ID == request.NotificationId && notification.UserId == currentUser.UserId, ct) ?? throw new NotFoundException("Notification not found.");
        if (!item.IsRead) { item.IsRead = true; item.ReadAt = item.UpdateAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
        await realtime.NotificationReadAsync(currentUser.UserId, item.ID, ct);
        await realtime.UnreadCountUpdatedAsync(currentUser.UserId, await db.Notifications.CountAsync(notification => notification.UserId == currentUser.UserId && !notification.IsRead, ct), ct);
    }
}

public sealed class MarkAllNotificationsReadHandler(AppDbContext db, ICurrentUser currentUser, INotificationRealtimePublisher realtime) : IRequestHandler<MarkAllNotificationsReadCommand>
{
    public async Task Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await db.Notifications.Where(item => item.UserId == currentUser.UserId && !item.IsRead).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsRead, true).SetProperty(item => item.ReadAt, now).SetProperty(item => item.UpdateAt, now), ct);
        await realtime.NotificationReadAsync(currentUser.UserId, null, ct);
        await realtime.UnreadCountUpdatedAsync(currentUser.UserId, 0, ct);
    }
}
