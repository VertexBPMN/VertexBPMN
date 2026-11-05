namespace VertexBPMN.Studio.Services;

internal sealed record PreviewNode(
    string Type,
    string? Label,
    string? DataSource,
    bool HasAction,
    bool ActionIsSideEffecting,
    PreviewKind Kind,
    IReadOnlyList<PreviewNode> Children);
