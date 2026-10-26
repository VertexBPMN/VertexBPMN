namespace VertexBPMN.Studio.Services;

internal sealed record PreviewPage(string Id, string Route, IReadOnlyList<PreviewNode> Components);
