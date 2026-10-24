using System.ComponentModel.DataAnnotations;

namespace VertexBPMN.Studio.Services;

/// <summary>
/// Persistenz-Eintrag einer geteilten OIDC-Sitzung (P4.5). Der Token-Satz wird
/// verschlüsselt (DataProtection) gespeichert; <see cref="Revision"/> dient als
/// Fencing-Zähler für verteiltes Refresh, <see cref="ExpiresAtUtc"/> als TTL.
/// </summary>
public sealed class OidcSessionRecord
{
    [MaxLength(64)]
    public string SessionId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>DataProtection-verschlüsselter Token-Satz (JSON).</summary>
    public byte[] ProtectedPayload { get; set; } = [];

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public long Revision { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
