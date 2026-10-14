namespace VertexBPMN.Studio.Services;

/// <summary>Result of a Studio mapper preview request.</summary>
internal sealed record StudioFieldMappingPreviewResult(
	bool Success,
	IReadOnlyDictionary<string, object?> Values,
	IReadOnlyList<string> Errors);
