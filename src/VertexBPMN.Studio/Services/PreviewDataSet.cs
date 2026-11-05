namespace VertexBPMN.Studio.Services;

/// <summary>A redacted test-data snapshot for one bound data source in the preview.</summary>
internal sealed record PreviewDataSet(string Key, int BindingVersion, IReadOnlyList<string> Fields, IReadOnlyList<IReadOnlyList<string>> RedactedRows);
