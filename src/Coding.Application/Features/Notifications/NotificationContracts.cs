using Coding.Enums;
using MediatR;

namespace Coding.Application.Features.Notifications;

public sealed record NotificationItem(Guid Id, Guid UserId, NotificationType Type, string Title, string Message, Guid? RelatedEntityId, string? RelatedEntityType, bool IsRead, DateTime CreatedAt, DateTime? ReadAt);
public sealed record NotificationPage(IReadOnlyList<NotificationItem> Items, string? NextCursor, int UnreadCount);
public sealed record CreateNotificationRequest(Guid UserId, NotificationType Type, string Title, string Message, Guid? RelatedEntityId = null, string? RelatedEntityType = null);
public sealed record GetNotificationsQuery(string? Cursor, int Limit = 30, bool? IsRead = null) : IRequest<NotificationPage>;
public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest;
public sealed record MarkAllNotificationsReadCommand : IRequest;

public interface INotificationService
{
    Task<NotificationItem?> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationItem>> CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken cancellationToken = default);
    Task MarkRelatedReadAsync(Guid userId, NotificationType type, Guid relatedEntityId, CancellationToken cancellationToken = default);
}

public interface INotificationRealtimePublisher
{
    Task NotificationReceivedAsync(NotificationItem notification, CancellationToken cancellationToken);
    Task NotificationReadAsync(Guid userId, Guid? notificationId, CancellationToken cancellationToken);
    Task UnreadCountUpdatedAsync(Guid userId, int count, CancellationToken cancellationToken);
}
