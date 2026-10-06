using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public enum WorkflowTriggerInvocationStatus
{
	Started,
	NotFound,
	InvalidSecret,
	Disabled,
	InvalidPayload,
	ProcessDefinitionNotFound,
	ReplayRejected
}
