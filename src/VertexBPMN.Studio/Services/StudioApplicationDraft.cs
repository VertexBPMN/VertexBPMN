namespace VertexBPMN.Studio.Services;

/// <summary>
/// A Studio application draft: its metadata plus the Current ContentJson (the latest
/// persisted revision, which the designer edits and resaves).
/// </summary>
internal sealed record StudioApplicationDraft(string Id, string Key, string Name, int Version, string Content);
