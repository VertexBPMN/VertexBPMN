namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Resolved server-side binding. CredentialReference is an ID, never a token or password.</summary>
public sealed record RepositoryBinding(Guid Id, string TenantId, Uri Remote,
    string? CredentialReference, string DefaultBranch, string ReleaseBranch,
    IReadOnlyList<string> ModelRoots);
