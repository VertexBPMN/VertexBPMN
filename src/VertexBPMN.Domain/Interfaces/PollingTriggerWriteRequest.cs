namespace VertexBPMN.Domain.Interfaces;

public sealed record PollingTriggerWriteRequest(
	string Name,
	string ProcessDefinitionKey,
	string ConnectorType,
	string? ConnectorAttributesJson = null,
	string? CredentialId = null,
	int? IntervalSeconds = null,
	bool? Enabled = null);
