namespace VertexBPMN.Studio.Services;

public sealed record ConfiguredConnectorTemplate(
    StudioConnectorTemplate Template,
    IReadOnlyDictionary<string, string> Values);
