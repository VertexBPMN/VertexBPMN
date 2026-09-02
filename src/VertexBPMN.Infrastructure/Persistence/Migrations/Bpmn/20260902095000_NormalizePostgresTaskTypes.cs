using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260902095000_NormalizePostgresTaskTypes")]
public sealed class NormalizePostgresTaskTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Tasks"
                ALTER COLUMN "MultiInstanceExecutionId" TYPE uuid
                USING NULLIF("MultiInstanceExecutionId", '')::uuid;
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Tasks"
                ALTER COLUMN "MultiInstanceExecutionId" TYPE text
                USING "MultiInstanceExecutionId"::text;
                """);
        }
    }
}
