namespace VertexBPMN.Domain.Interfaces;

public sealed record CredentialWriteRequest(
	string Name,
	string Type,
	string? Description,
	IReadOnlyDictionary<string, string> Secrets);
