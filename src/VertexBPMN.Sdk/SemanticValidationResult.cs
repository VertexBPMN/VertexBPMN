namespace VertexBPMN.Sdk;
public sealed record SemanticValidationResult(bool IsValid, IReadOnlyList<string>? Errors, IReadOnlyList<string>? Warnings, IReadOnlyList<string>? Suggestions);
