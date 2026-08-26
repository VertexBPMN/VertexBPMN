using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260819130000_AddWebhookTriggerMetadata")]
public partial class AddWebhookTriggerMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Path", "WorkflowTriggers", maxLength: 512, nullable: true);
        migrationBuilder.AddColumn<string>("Method", "WorkflowTriggers", maxLength: 16, nullable: true);
        migrationBuilder.AddColumn<string>("AuthenticationMode", "WorkflowTriggers", maxLength: 32, nullable: false, defaultValue: "trigger-secret");
        migrationBuilder.AddColumn<string>("CredentialId", "WorkflowTriggers", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>("CredentialSecretKey", "WorkflowTriggers", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>("PayloadSchemaJson", "WorkflowTriggers", nullable: true);
        migrationBuilder.AddColumn<string>("CorrelationKey", "WorkflowTriggers", maxLength: 256, nullable: true);
        migrationBuilder.AddColumn<string>("SourceElementId", "WorkflowTriggers", nullable: true);
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
