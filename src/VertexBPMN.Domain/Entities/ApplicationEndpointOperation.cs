namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Durable acceptance and frozen release target for an application-endpoint process start.
/// Started confirms only the start call; uncertain Running operations are not retried here.
/// </summary>
public sealed class ApplicationEndpointOperation
{
    public Guid Id { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ApplicationKey { get; set; } = string.Empty;
    public string EndpointKey { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string? ApplicationDefinitionId { get; set; }
    public string? ApplicationRevisionId { get; set; }
    public string? ApplicationReleaseId { get; set; }
    public string? BoundProcessDefinitionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProcessInstanceId { get; set; }
    public string? ResultJson { get; set; }
    public string? ErrorMessage { get; set; }
    public long CreatedAtTicks { get; set; }
    public long UpdatedAtTicks { get; set; }
}
