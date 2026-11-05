namespace VertexBPMN.Domain.Entities;

/// <summary>References an existing page action, not an arbitrary execution target.</summary>
public sealed record ApplicationEndpointActionBinding(string PageId, string Type, string Version, string Target);
