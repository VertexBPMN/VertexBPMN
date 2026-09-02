using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Messaging;

/// <summary>
/// Basic notification service: currently just logs; can be extended to SignalR, email, etc.
/// </summary>
public class NotificationService(ILogger<NotificationService> logger, IMessageDispatcher? dispatcher)
    : INotificationService
{

    public async Task SendNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
    {
        foreach (var n in notifications)
        {
            logger.LogInformation("Notify {Recipient}: {Message}", n.RecipientId, n.Message);
            if (dispatcher is null) continue;
            await dispatcher.DispatchUserTaskAsync(n.RecipientId, "notification", new Dictionary<string, object>
            {
                { "message", n.Message },
                { "category", n.Category ?? "general" },
                { "timestamp", n.Timestamp }
            }, cancellationToken);
            await Task.Yield();
        }
    }
}
