using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260902094000_NormalizePostgresRuntimeOutboxTypes")]
public sealed class NormalizePostgresRuntimeOutboxTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "RuntimeOutbox"
                ALTER COLUMN "AnalyticsProjectedAt" TYPE timestamp with time zone
                USING NULLIF("AnalyticsProjectedAt", '')::timestamp with time zone;
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "RuntimeOutbox"
                ALTER COLUMN "AnalyticsProjectedAt" TYPE text
                USING "AnalyticsProjectedAt"::text;
                """);
        }
    }
}
