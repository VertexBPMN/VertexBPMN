using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public interface INotificationService
{
    Task SendNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);
}