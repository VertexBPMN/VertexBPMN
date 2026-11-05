using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable
#pragma warning disable MA0048 // The migration file name is the canonical identifier.

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

/// <inheritdoc />
[DbContext(typeof(BpmnDbContext))]
[Migration("20261026060100_LC08ExternalTaskReleaseStamp")]
public class LC08ExternalTaskReleaseStamp : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ApplicationReleaseId",
            table: "ExternalTaskJobs",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Bound external-task jobs keep their release/flow-revision stamp for the
        // lifetime of the run; refuse to drop the column while any job still exists,
        // mirroring the ExternalTaskPersistence downgrade guard.
        migrationBuilder.Sql("CREATE TEMPORARY TABLE ExternalTaskDowngradeGuard (CountValue INTEGER CHECK (CountValue = 0));");
        migrationBuilder.Sql("INSERT INTO ExternalTaskDowngradeGuard SELECT COUNT(*) FROM \"ExternalTaskJobs\";");
        migrationBuilder.Sql("DROP TABLE ExternalTaskDowngradeGuard;");
        migrationBuilder.Sql("ALTER TABLE \"ExternalTaskJobs\" DROP COLUMN \"ApplicationReleaseId\";");
    }
}
