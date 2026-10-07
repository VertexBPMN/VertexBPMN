namespace VertexBPMN.Domain.Interfaces;

/// <summary>Identity comes from authenticated claims; browser proof is a random, per-flow secret.</summary>
public sealed record OAuth2FlowBinding(string Subject, string BrowserProof);
