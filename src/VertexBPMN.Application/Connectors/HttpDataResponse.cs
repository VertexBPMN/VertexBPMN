namespace VertexBPMN.Application.Connectors;

public sealed record HttpDataResponse(
	string? ContentType,
	string? EncodingName,
	string RawBody,
	bool IsJson,
	bool Success,
	string? ErrorCode,
	IReadOnlyDictionary<string, object> Fields);
