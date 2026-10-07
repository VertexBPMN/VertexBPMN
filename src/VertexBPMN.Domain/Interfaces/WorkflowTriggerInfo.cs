using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public sealed record WorkflowTriggerInfo(
	Guid Id,
	string Name,
	string ProcessDefinitionKey,
	string? TenantId,
	bool Enabled,
	DateTime CreatedAt,
	DateTime LastModified,
	DateTime? LastTriggeredAt,
	long InvocationCount,
	string? Path,
	string? Method,
	string AuthenticationMode,
	string? CredentialId,
	string? CorrelationKey);
