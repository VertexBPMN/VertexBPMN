using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public sealed record WorkflowTriggerCreated(WorkflowTriggerInfo Trigger, string Secret, string InvokePath);
