using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class AddFormDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthenticationMode",
                table: "WorkflowTriggers",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CorrelationKey",
                table: "WorkflowTriggers",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialId",
                table: "WorkflowTriggers",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialSecretKey",
                table: "WorkflowTriggers",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "WorkflowTriggers",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "WorkflowTriggers",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayloadSchemaJson",
                table: "WorkflowTriggers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceElementId",
                table: "WorkflowTriggers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateId",
                table: "Connectors",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConnectorTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AppliesToJson = table.Column<string>(type: "TEXT", nullable: false),
                    Runtime = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PropertiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FormDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Schema = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTriggers_Path_Method",
                table: "WorkflowTriggers",
                columns: new[] { "Path", "Method" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTriggers_TenantId_ProcessDefinitionKey_SourceElementId",
                table: "WorkflowTriggers",
                columns: new[] { "TenantId", "ProcessDefinitionKey", "SourceElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Connectors_TemplateId",
                table: "Connectors",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorTemplates_TenantId",
                table: "ConnectorTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorTemplates_TenantId_Name",
                table: "ConnectorTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_TenantId_Key",
                table: "FormDefinitions",
                columns: new[] { "TenantId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectorTemplates");

            migrationBuilder.DropTable(
                name: "FormDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowTriggers_Path_Method",
                table: "WorkflowTriggers");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowTriggers_TenantId_ProcessDefinitionKey_SourceElementId",
                table: "WorkflowTriggers");

            migrationBuilder.DropIndex(
                name: "IX_Connectors_TemplateId",
                table: "Connectors");

            migrationBuilder.DropColumn(
                name: "AuthenticationMode",
                table: "WorkflowTriggers");

            migrationBuilder.DropColumn(
                name: "CorrelationKey",
                table: "WorkflowTriggers");

            migrationBuilder.DropColumn(
                name: "CredentialId",
                table: "WorkflowTriggers");

            migrationBuilder.DropColumn(
                name: "CredentialSecretKey",
                table: "WorkflowTriggers");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "WorkflowTriggers");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "WorkflowTriggers");

            migrationBuilder.DropColumn(
                name: "PayloadSchemaJson",
                table: "WorkflowTriggers");

            migrationBuilder.DropColumn(
                name: "SourceElementId",
                table: "WorkflowTriggers");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Connectors");
        }
    }
}
