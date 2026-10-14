namespace VertexBPMN.Application.Connectors;

/// <summary>Outcome of applying a set of <see cref="FieldMapping"/>s to one source value.</summary>
/// <param name="Success">True when every mapping applied; when false, <see cref="Values"/> is empty (no partial assignment).</param>
/// <param name="Values">Produced target values (only populated on success).</param>
/// <param name="Errors">Per-target errors (only populated on failure).</param>
public sealed record FieldMappingResult(bool Success, IReadOnlyDictionary<string, object?> Values, IReadOnlyList<string> Errors);
