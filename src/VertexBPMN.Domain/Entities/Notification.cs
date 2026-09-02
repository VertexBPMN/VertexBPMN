namespace VertexBPMN.Domain.Entities;

public record Notification(
    string RecipientId,
    string Message,
    string? Category = null,
    DateTime Timestamp = default)
{
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}