namespace VertexBPMN.Domain.Entities;

/// <summary>Trusted server-side identity and grants; never construct from draft metadata.</summary>
public sealed record ApplicationAccessContext(string ActorId, IReadOnlySet<string> Permissions);
