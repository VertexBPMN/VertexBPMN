namespace VertexBPMN.Domain.Interfaces;

public sealed record PollingTriggerInfo(
	Guid Id,
	string TenantId,
	string Name,
	string ProcessDefinitionKey,
	string ConnectorType,
	string ConnectorAttributesJson,
	string? CredentialId,
	int IntervalSeconds,
	string CursorStateJson,
	bool Enabled,
	DateTime? NextDueAt,
	DateTime? LastPolledAt,
	int ConsecutiveFailures,
	DateTime CreatedAt,
	DateTime LastModified);
