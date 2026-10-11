namespace VertexBPMN.Studio.Services;

internal sealed class StudioWorkflowTriggerCreated
{
    public StudioWorkflowTrigger Trigger { get; set; } = new();
    public string Secret { get; set; } = string.Empty;
    public string InvokePath { get; set; } = string.Empty;
}
