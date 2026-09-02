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
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 256, nullable: false),
                ProcessDefinitionKey = table.Column<string>(maxLength: 255, nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: true),
                SecretHash = table.Column<string>(maxLength: 128, nullable: false),
                Enabled = table.Column<bool>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                LastModified = table.Column<DateTime>(nullable: false),
                LastTriggeredAt = table.Column<DateTime>(nullable: true),
                InvocationCount = table.Column<long>(nullable: false)
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
