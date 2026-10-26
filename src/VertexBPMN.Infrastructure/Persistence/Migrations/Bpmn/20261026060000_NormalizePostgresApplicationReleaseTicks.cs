using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861, MA0048, MA0051, CA1814

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

/// <summary>
/// Repairs the ApplicationReleases column types created by the SQLite-authored migration
/// 20261026052338_ApplicationReleaseActivation when that chain is applied to PostgreSQL:
/// the Ticks/PointerRevision (long) columns were authored as "INTEGER" (32-bit on
/// PostgreSQL) but the durable release record stores DateTime.UtcNow.Ticks (~6.4e17),
/// which overflows an int32. Promotes them to bigint on PostgreSQL.
/// </summary>
[DbContext(typeof(BpmnDbContext))]
[Migration("20261026060000_NormalizePostgresApplicationReleaseTicks")]
public sealed class NormalizePostgresApplicationReleaseTicks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider?.Contains("Npgsql", StringComparison.Ordinal) != true)
        {
            return;
        }

        migrationBuilder.Sql(
            """
            ALTER TABLE "ApplicationReleases"
                ALTER COLUMN "PointerRevision" TYPE bigint USING "PointerRevision"::bigint,
                ALTER COLUMN "PreparedAtTicks" TYPE bigint USING "PreparedAtTicks"::bigint,
                ALTER COLUMN "DeployStartedAtTicks" TYPE bigint USING "DeployStartedAtTicks"::bigint,
                ALTER COLUMN "ActivatedAtTicks" TYPE bigint USING "ActivatedAtTicks"::bigint,
                ALTER COLUMN "UpdatedAtTicks" TYPE bigint USING "UpdatedAtTicks"::bigint;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider?.Contains("Npgsql", StringComparison.Ordinal) != true)
        {
            return;
        }

        migrationBuilder.Sql(
            """
            ALTER TABLE "ApplicationReleases"
                ALTER COLUMN "PointerRevision" TYPE integer USING "PointerRevision"::integer,
                ALTER COLUMN "PreparedAtTicks" TYPE integer USING "PreparedAtTicks"::integer,
                ALTER COLUMN "DeployStartedAtTicks" TYPE integer USING "DeployStartedAtTicks"::integer,
                ALTER COLUMN "ActivatedAtTicks" TYPE integer USING "ActivatedAtTicks"::integer,
                ALTER COLUMN "UpdatedAtTicks" TYPE integer USING "UpdatedAtTicks"::integer;
            """);
    }
}
