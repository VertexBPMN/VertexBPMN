namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Temporärer Zustand eines laufenden OAuth2-Authorization-Code-Flows.
/// Wird beim Abschluss (Callback) gelöscht; verfälschte Einträge werden vom Cleanup-Service entfernt.
/// </summary>
public sealed class OAuth2FlowStateRecord
{
    public string State { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
