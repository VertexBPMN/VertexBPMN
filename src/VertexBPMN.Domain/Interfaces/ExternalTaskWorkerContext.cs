using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskWorkerContext(
	string Issuer,
	string Subject,
	string TenantId,
	IReadOnlySet<string> Topics,
	IReadOnlySet<string> Profiles);
