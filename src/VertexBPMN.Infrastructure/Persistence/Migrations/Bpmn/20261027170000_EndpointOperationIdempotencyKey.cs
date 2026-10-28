using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048", Justification = "EF migrations use timestamp-prefixed file names.")]
[DbContext(typeof(BpmnDbContext))]
[Migration("20261027170000_EndpointOperationIdempotencyKey")]
public sealed class EndpointOperationIdempotencyKey : Migration
{
    private const string Table = "ApplicationEndpointOperations";
    private const string KeyColumn = "IdempotencyKey";
    private const string IndexName =
        "IX_ApplicationEndpointOperations_TenantId_ApplicationKey_EndpointKey_IdempotencyKey";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Optional client idempotency key: NULL keeps the pre-existing per-call behavior
        // (each POST without a key creates a fresh operation).
        migrationBuilder.AddColumn<string>(KeyColumn, Table, type: "TEXT", nullable: true);
        // Unique per (tenant, app, endpoint, key) so repeated identical POSTs collapse to a
        // single Accepted handle; SQL treats multiple NULL keys as distinct rows.
        migrationBuilder.CreateIndex(
            name: IndexName,
            table: Table,
            columns: new[] { "TenantId", "ApplicationKey", "EndpointKey", KeyColumn },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: IndexName, table: Table);
        migrationBuilder.DropColumn(KeyColumn, Table);
    }
}
