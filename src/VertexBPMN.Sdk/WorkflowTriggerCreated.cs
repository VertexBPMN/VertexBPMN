namespace VertexBPMN.Sdk;

public sealed record WorkflowTriggerCreated(
    WorkflowTrigger Trigger,
    string Secret,
    string InvokePath);
