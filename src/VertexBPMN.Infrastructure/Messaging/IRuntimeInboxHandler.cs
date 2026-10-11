using System.Text.Json;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>Idempotent business handler invoked exactly once per stable message id.</summary>
public interface IRuntimeInboxHandler
{
    Task HandleAsync(string? eventType, JsonElement? payload, Guid? processInstanceId, string? tenantId, CancellationToken cancellationToken);
}
