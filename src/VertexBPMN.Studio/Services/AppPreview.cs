namespace VertexBPMN.Studio.Services;

/// <summary>The read-only preview of the current draft: pages + redacted test data, never executable.</summary>
internal sealed record AppPreview(
    PreviewTestTarget Target,
    IReadOnlyList<PreviewPage> Pages,
    IReadOnlyList<PreviewDataSet> DataSets,
    bool ReadOnly,
    int UnsupportedCount,
    IReadOnlyList<string> ExecutableActions);
