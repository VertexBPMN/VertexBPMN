namespace VertexBPMN.Studio.Services;

/// <summary>Lightweight application-metadata listing row for the Studio Applications page.</summary>
internal sealed record StudioApplicationSummary(string Id, string Key, string Name, int Version);
