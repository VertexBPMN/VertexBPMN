using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public sealed record WorkflowTriggerInvocationResult(
	WorkflowTriggerInvocationStatus Status,
	ProcessInstance? ProcessInstance = null);
