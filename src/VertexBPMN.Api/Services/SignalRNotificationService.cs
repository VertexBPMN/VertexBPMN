using Microsoft.AspNetCore.SignalR;
using VertexBPMN.Api.Hubs;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Services;

public sealed class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<ProcessMonitoringHub> _hub;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<ProcessMonitoringHub> hub,
        ILogger<SignalRNotificationService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task SendNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
    {
        // Broadcast each notification to a per-user group plus a generic stream
        foreach (var n in notifications)
        {
            var payload = new
            {
                Type = "UserNotification",
                Recipient = n.RecipientId,
                Message = n.Message,
                Category = n.Category ?? "general",
                Timestamp = n.Timestamp
            };

            // Per-user group (server side you must add connections to "User_<id>")
            await _hub.Clients.Group($"User_{n.RecipientId}")
                .SendAsync("UserNotification", payload, cancellationToken);

            // Global stream (optional)
            await _hub.Clients.Group("Notifications")
                .SendAsync("UserNotification", payload, cancellationToken);

            _logger.LogDebug("Sent notification to {Recipient} ({Category})", n.RecipientId, n.Category);
        }
    }
}