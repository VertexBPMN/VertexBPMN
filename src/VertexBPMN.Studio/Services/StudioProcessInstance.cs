namespace VertexBPMN.Studio.Services;

internal sealed class StudioProcessInstance
{
    public Guid Id { get; set; }
    public string ProcessId { get; set; } = string.Empty;
    public string? BusinessKey { get; set; }
    public string? TenantId { get; set; }
    public string State { get; set; } = string.Empty;
}
