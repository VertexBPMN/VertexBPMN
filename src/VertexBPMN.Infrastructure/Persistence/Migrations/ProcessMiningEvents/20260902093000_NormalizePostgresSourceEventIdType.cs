using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence.Services;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.ProcessMiningEvents;

[DbContext(typeof(ProcessMiningEventDbContext))]
[Migration("20260902093000_NormalizePostgresSourceEventIdType")]
public sealed class NormalizePostgresSourceEventIdType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Events"
                ALTER COLUMN "SourceEventId" TYPE uuid
                USING NULLIF("SourceEventId", '')::uuid;
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Events"
                ALTER COLUMN "SourceEventId" TYPE text
                USING "SourceEventId"::text;
                """);
        }
    }
}
