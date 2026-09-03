using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence.Services;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.ProcessMiningEvents;

[DbContext(typeof(ProcessMiningEventDbContext))]
[Migration("20260903113000_AdvancePostgresEventIdentity")]
public sealed class AdvancePostgresEventIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            migrationBuilder.Sql(
                """
                SELECT setval(
                    pg_get_serial_sequence('"Events"', 'Id'),
                    GREATEST(COALESCE(MAX("Id"), 0), 1),
                    true)
                FROM "Events";
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
