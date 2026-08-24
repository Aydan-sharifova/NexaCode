using System.Security.Cryptography;
using System.Text;

namespace Coding.Application.Features.Notifications;

public static class NotificationDeduplication
{
    public static string Key(CreateNotificationRequest request,DateTime utcNow)
    {
        if(!string.IsNullOrWhiteSpace(request.DeduplicationKey))return Hash(request.DeduplicationKey.Trim());
        var minute=new DateTime(utcNow.Year,utcNow.Month,utcNow.Day,utcNow.Hour,utcNow.Minute,0,DateTimeKind.Utc);
        return Hash($"{request.UserId:N}|{request.Type}|{request.RelatedEntityId:N}|{request.RelatedEntityType}|{request.Title}|{request.Message}|{minute:O}");
    }
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
