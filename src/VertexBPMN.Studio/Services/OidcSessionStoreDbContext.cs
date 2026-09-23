using Microsoft.EntityFrameworkCore;

namespace VertexBPMN.Studio.Services;

/// <summary>
/// Providerneutrale Persistenz für den geteilten OIDC-Session-Store (P4.5).
/// SQLite bleibt der lokale Entwicklungsfallback; PostgreSQL ist das Azure-Ziel
/// für geteilten, replikaübergreifenden Sitzungszustand.
/// </summary>
public sealed class OidcSessionStoreDbContext(DbContextOptions<OidcSessionStoreDbContext> options) : DbContext(options)
{
    public DbSet<OidcSessionRecord> Sessions => Set<OidcSessionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OidcSessionRecord>(entity =>
        {
            entity.ToTable("OidcSessions");
            entity.HasKey(record => record.SessionId);
            entity.Property(record => record.SessionId).HasMaxLength(64);
            entity.Property(record => record.Subject).HasMaxLength(256).IsRequired();
            entity.Property(record => record.ProtectedPayload).IsRequired();
            entity.Property(record => record.ExpiresAtUtc);
            entity.Property(record => record.Revision);
            entity.Property(record => record.UpdatedAtUtc);
            entity.HasIndex(record => record.ExpiresAtUtc);
        });
    }
}
