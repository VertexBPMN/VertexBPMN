using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

/// <summary>
/// Repairs the CMMN case-instance table created by the original SQLite-authored
/// migration when that migration is applied to PostgreSQL.
/// </summary>
[DbContext(typeof(BpmnDbContext))]
[Migration("20260902090000_NormalizePostgresCmmnCaseInstanceTypes")]
public sealed class NormalizePostgresCmmnCaseInstanceTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
            return;

        migrationBuilder.Sql(
            """
            ALTER TABLE "CaseInstances"
                ALTER COLUMN "Id" TYPE uuid USING "Id"::uuid,
                ALTER COLUMN "CreatedAt" TYPE timestamp with time zone USING "CreatedAt"::timestamp with time zone,
                ALTER COLUMN "LastModified" TYPE timestamp with time zone USING "LastModified"::timestamp with time zone,
                ALTER COLUMN "CompletedAt" TYPE timestamp with time zone USING NULLIF("CompletedAt", '')::timestamp with time zone,
                ALTER COLUMN "Revision" TYPE bigint USING "Revision"::bigint;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
            return;

        migrationBuilder.Sql(
            """
            ALTER TABLE "CaseInstances"
                ALTER COLUMN "Id" TYPE text USING "Id"::text,
                ALTER COLUMN "CreatedAt" TYPE text USING "CreatedAt"::text,
                ALTER COLUMN "LastModified" TYPE text USING "LastModified"::text,
                ALTER COLUMN "CompletedAt" TYPE text USING "CompletedAt"::text,
                ALTER COLUMN "Revision" TYPE integer USING "Revision"::integer;
            """);
    }
}
