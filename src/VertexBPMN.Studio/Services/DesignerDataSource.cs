namespace VertexBPMN.Studio.Services;

/// <summary>
/// A data source declared in the low-code app designer draft (LC07 S2). It carries the
/// released LC06 identity <c>key</c> + <c>bindingVersion</c> (matching the Lc06ReferenceApp
/// dataSources shape so a later publish slice can emit a valid released application) plus
/// a design-time <see cref="Fields"/> schema that the designer validates field mappings
/// against. The released readers only consume <c>key</c>/<c>bindingVersion</c>; the fields
/// are authored schema metadata that round-trips through the draft.
/// </summary>
internal sealed record DesignerDataSource(string Key, int BindingVersion, IReadOnlyList<string> Fields);
