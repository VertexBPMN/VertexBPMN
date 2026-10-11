namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>A complete rejection from the single push attempt proves no ref update for that attempt.</summary>
public sealed class RejectedRemotePushException() : Exception("Remote push rejected.");
