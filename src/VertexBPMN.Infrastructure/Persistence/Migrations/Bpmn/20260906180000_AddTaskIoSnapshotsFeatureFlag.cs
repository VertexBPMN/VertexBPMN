using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

/// <summary>
/// Seeds the opt-in <c>task-io-snapshots</c> feature flag (disabled by default so
/// existing tenants are unaffected; an admin enables it via the feature-flag API to
/// begin recording redacted task-IO snapshots).
/// </summary>
[DbContext(typeof(VertexBPMN.Infrastructure.Persistence.BpmnDbContext))]
[Migration("20260906180000_AddTaskIoSnapshotsFeatureFlag")]
public partial class AddTaskIoSnapshotsFeatureFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Provider-specific literal: EFCore maps bool to INTEGER on SQLite and to
        // boolean on PostgreSQL. Data-only migrations here use raw SQL (see the
        // NormalizePostgres* migrations) because InsertData ops need a Designer
        // model snapshot to resolve the target table.
        migrationBuilder.Sql(
            ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
                ? "INSERT INTO \"FeatureFlags\" (\"Name\", \"Enabled\") VALUES ('task-io-snapshots', false);"
                : "INSERT INTO \"FeatureFlags\" (\"Name\", \"Enabled\") VALUES ('task-io-snapshots', 0);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM \"FeatureFlags\" WHERE \"Name\" = 'task-io-snapshots';");
    }
}
