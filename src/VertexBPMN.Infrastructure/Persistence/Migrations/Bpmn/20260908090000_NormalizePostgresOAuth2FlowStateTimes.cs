using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260908090000_NormalizePostgresOAuth2FlowStateTimes")]
public sealed class NormalizePostgresOAuth2FlowStateTimes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider?.Contains("Npgsql", StringComparison.Ordinal) != true)
            return;

        // Legacy values were written as UTC. Explicit offsets remain authoritative;
        // offset-free values must not depend on the database server's local timezone.
        // Invalid values intentionally fail the transaction instead of deleting states.
        migrationBuilder.Sql("""
            SET LOCAL TIME ZONE 'UTC';
            ALTER TABLE "OAuth2FlowStates"
                ALTER COLUMN "CreatedAt" TYPE timestamp with time zone USING "CreatedAt"::timestamp with time zone,
                ALTER COLUMN "ExpiresAt" TYPE timestamp with time zone USING "ExpiresAt"::timestamp with time zone;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider?.Contains("Npgsql", StringComparison.Ordinal) != true)
            return;

        migrationBuilder.Sql("""
            SET LOCAL TIME ZONE 'UTC';
            ALTER TABLE "OAuth2FlowStates"
                ALTER COLUMN "CreatedAt" TYPE text USING "CreatedAt"::text,
                ALTER COLUMN "ExpiresAt" TYPE text USING "ExpiresAt"::text;
            """);
    }
}
