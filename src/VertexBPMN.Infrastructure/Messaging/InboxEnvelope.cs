using System.Text.Json;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Parsed shape of the runtime outbox envelope published by the transport.
/// The payload is <see cref="JsonElement.Clone()"/>d so it outlives the source
/// <see cref="JsonDocument"/> used to parse the wire body.
/// </summary>
public sealed record InboxEnvelope(
    Guid? Id,
    string? EventType,
    Guid? ProcessInstanceId,
    string? TenantId,
    DateTimeOffset? OccurredAt,
    JsonElement? Payload)
{
    public static InboxEnvelope Parse(byte[] body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Guid? id = null;
        if (root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
            && Guid.TryParse(idElement.GetString(), out var parsedId))
        {
            id = parsedId;
        }

        var eventType = root.TryGetProperty("eventType", out var eventTypeElement)
            ? eventTypeElement.GetString()
            : null;

        Guid? processInstanceId = null;
        if (root.TryGetProperty("processInstanceId", out var processInstanceIdElement)
            && processInstanceIdElement.ValueKind == JsonValueKind.String)
        {
            processInstanceId = Guid.Parse(processInstanceIdElement.GetString()!);
        }

        string? tenantId = null;
        if (root.TryGetProperty("tenantId", out var tenantIdElement)
            && tenantIdElement.ValueKind is JsonValueKind.String or JsonValueKind.Null)
        {
            tenantId = tenantIdElement.ValueKind == JsonValueKind.Null ? null : tenantIdElement.GetString();
        }

        DateTimeOffset? occurredAt = root.TryGetProperty("occurredAt", out var occurredAtElement)
            ? (DateTimeOffset?)occurredAtElement.GetDateTimeOffset()
            : null;

        JsonElement? payload = root.TryGetProperty("payload", out var payloadElement)
            ? payloadElement.Clone()
            : null;

        return new InboxEnvelope(id, eventType, processInstanceId, tenantId, occurredAt, payload);
    }
}
