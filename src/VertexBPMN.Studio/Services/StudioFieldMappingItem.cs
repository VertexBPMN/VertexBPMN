namespace VertexBPMN.Studio.Services;

/// <summary>One explicit field mapping edited in the Studio mapper (LC03 Z.247).</summary>
internal sealed record StudioFieldMappingItem(string SourcePath, string TargetKey, string? Transform, bool Required);
