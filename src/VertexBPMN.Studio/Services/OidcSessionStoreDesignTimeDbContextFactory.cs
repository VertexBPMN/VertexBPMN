using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VertexBPMN.Studio.Services;

/// <summary>
/// Design-Zeit-Factory für <c>dotnet ef migrations</c>. Authoring erfolgt wie im Rest des
/// Repos gegen SQLite (kanonisches Modell); PostgreSQL/Kompatibilität wird im Acceptance-Test
/// über den vollständigen Migrationspfad nachgewiesen.
/// </summary>
public sealed class OidcSessionStoreDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OidcSessionStoreDbContext>
{
    public OidcSessionStoreDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OidcSessionStoreDbContext>()
            .UseSqlite("Data Source=vertexbpmn-oidc-sessions-design.db")
            .Options;
        return new OidcSessionStoreDbContext(options);
    }
}
