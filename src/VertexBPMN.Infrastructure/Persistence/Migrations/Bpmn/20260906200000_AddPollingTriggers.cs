using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260906200000_AddPollingTriggers")]
public partial class AddPollingTriggers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PollingTriggers",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: false),
                Name = table.Column<string>(maxLength: 256, nullable: false),
                ProcessDefinitionKey = table.Column<string>(maxLength: 256, nullable: false),
                ConnectorType = table.Column<string>(maxLength: 64, nullable: false),
                ConnectorAttributesJson = table.Column<string>(nullable: false),
                CredentialId = table.Column<string>(maxLength: 128, nullable: true),
                IntervalSeconds = table.Column<int>(nullable: false),
                CursorStateJson = table.Column<string>(nullable: false),
                Enabled = table.Column<bool>(nullable: false),
                NextDueAt = table.Column<DateTime>(nullable: true),
                LockOwner = table.Column<string>(maxLength: 128, nullable: true),
                LockedUntil = table.Column<DateTime>(nullable: true),
                LastPolledAt = table.Column<DateTime>(nullable: true),
                ConsecutiveFailures = table.Column<int>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                LastModified = table.Column<DateTime>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PollingTriggers", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_PollingTriggers_TenantId_Enabled_NextDueAt",
            table: "PollingTriggers",
            columns: new[] { "TenantId", "Enabled", "NextDueAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "PollingTriggers");
}
