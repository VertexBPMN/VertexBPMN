using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Notifications.Email;

/// <summary>
/// Decorates an existing INotificationService by adding an optional email channel.
/// 1. Invokes the inner service first (primary delivery).
/// 2. Then attempts to send the same batch via EmailNotificationService (auxiliary).
/// Email failures are logged but never block the primary path.
/// </summary>
public sealed class EmailNotificationDecoratingService : INotificationService
{
    private readonly INotificationService _inner;
    private readonly EmailNotificationService _email;
    private readonly ILogger<EmailNotificationDecoratingService> _logger;

    public EmailNotificationDecoratingService(INotificationService inner,
                                              EmailNotificationService email,
                                              ILogger<EmailNotificationDecoratingService> logger)
    {
        _inner = inner;
        _email = email;
        _logger = logger;
    }

    public async Task SendNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
    {
        var list = notifications as IList<Notification> ?? notifications.ToList();
        if (list.Count == 0)
            return;

        // Deliver through primary channel first
        await _inner.SendNotificationsAsync(list, cancellationToken);

        // Auxiliary email channel (swallow errors)
        try
        {
            await _email.SendNotificationsAsync(list, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email decorator failed for batch of {Count} notifications", list.Count);
        }
    }
}