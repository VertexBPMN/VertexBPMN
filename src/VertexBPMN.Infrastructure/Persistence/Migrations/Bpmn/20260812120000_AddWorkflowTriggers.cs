using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

public partial class AddWorkflowTriggers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WorkflowTriggers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ProcessDefinitionKey = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                SecretHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastTriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                InvocationCount = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowTriggers", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowTriggers_ProcessDefinitionKey",
            table: "WorkflowTriggers",
            column: "ProcessDefinitionKey");
        migrationBuilder.CreateIndex(
            name: "IX_WorkflowTriggers_TenantId",
            table: "WorkflowTriggers",
            column: "TenantId");
        migrationBuilder.CreateIndex(
            name: "IX_WorkflowTriggers_LastModified",
            table: "WorkflowTriggers",
            column: "LastModified");
        migrationBuilder.CreateIndex(
            name: "IX_WorkflowTriggers_TenantId_Name",
            table: "WorkflowTriggers",
            columns: new[] { "TenantId", "Name" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "WorkflowTriggers");
}
