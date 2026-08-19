using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[Migration("20260819130000_AddWebhookTriggerMetadata")]
public partial class AddWebhookTriggerMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Path", "WorkflowTriggers", type: "TEXT", maxLength: 512, nullable: true);
        migrationBuilder.AddColumn<string>("Method", "WorkflowTriggers", type: "TEXT", maxLength: 16, nullable: true);
        migrationBuilder.AddColumn<string>("AuthenticationMode", "WorkflowTriggers", type: "TEXT", maxLength: 32, nullable: false, defaultValue: "trigger-secret");
        migrationBuilder.AddColumn<string>("CredentialId", "WorkflowTriggers", type: "TEXT", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>("CredentialSecretKey", "WorkflowTriggers", type: "TEXT", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>("PayloadSchemaJson", "WorkflowTriggers", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>("CorrelationKey", "WorkflowTriggers", type: "TEXT", maxLength: 256, nullable: true);
        migrationBuilder.AddColumn<string>("SourceElementId", "WorkflowTriggers", type: "TEXT", nullable: true);
        migrationBuilder.CreateIndex("IX_WorkflowTriggers_Path_Method", "WorkflowTriggers", new[] { "Path", "Method" }, unique: true);
        migrationBuilder.CreateIndex("IX_WorkflowTriggers_TenantId_ProcessDefinitionKey_SourceElementId", "WorkflowTriggers", new[] { "TenantId", "ProcessDefinitionKey", "SourceElementId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_WorkflowTriggers_Path_Method", "WorkflowTriggers");
        migrationBuilder.DropIndex("IX_WorkflowTriggers_TenantId_ProcessDefinitionKey_SourceElementId", "WorkflowTriggers");
        migrationBuilder.DropColumn("Path", "WorkflowTriggers"); migrationBuilder.DropColumn("Method", "WorkflowTriggers");
        migrationBuilder.DropColumn("AuthenticationMode", "WorkflowTriggers"); migrationBuilder.DropColumn("CredentialId", "WorkflowTriggers");
        migrationBuilder.DropColumn("CredentialSecretKey", "WorkflowTriggers"); migrationBuilder.DropColumn("PayloadSchemaJson", "WorkflowTriggers");
        migrationBuilder.DropColumn("CorrelationKey", "WorkflowTriggers"); migrationBuilder.DropColumn("SourceElementId", "WorkflowTriggers");
    }
}
