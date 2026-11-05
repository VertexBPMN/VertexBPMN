namespace VertexBPMN.Application.Connectors;

/// <summary>
/// One explicit input→output mapping in the Studio mapper. <see cref="SourcePath"/> may be
/// dotted (e.g. <c>httpBody.customer</c> or <c>rows.0.name</c>) to resolve nested dict/list values.
/// </summary>
/// <param name="SourcePath">Dotted path into the source value (source field to read).</param>
/// <param name="TargetKey">Output variable/target key the resolved value is written to.</param>
/// <param name="Transform">Explicit transformation (defaults to <see cref="FieldTransform.Copy"/>).</param>
/// <param name="Required">When true, a missing or null source aborts the whole mapping (no partial assignment).</param>
public sealed record FieldMapping(string SourcePath, string TargetKey, FieldTransform Transform = FieldTransform.Copy, bool Required = false);
