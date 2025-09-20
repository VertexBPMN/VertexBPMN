using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Decision
{
    /// <inheritdoc />
    public partial class InitDecisionStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DecisionDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DmnXml = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DecisionInstances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DecisionDefinitionKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EvaluationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InputVariables = table.Column<string>(type: "TEXT", nullable: false),
                    OutputVariables = table.Column<string>(type: "TEXT", nullable: false),
                    Failed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DmnDecisionTables",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Inputs = table.Column<string>(type: "TEXT", nullable: false),
                    Outputs = table.Column<string>(type: "TEXT", nullable: false),
                    Rules = table.Column<string>(type: "TEXT", nullable: false),
                    HitPolicy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DmnDecisionTables", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionDefinitions_Key",
                table: "DecisionDefinitions",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionDefinitions_Key_TenantId",
                table: "DecisionDefinitions",
                columns: new[] { "Key", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DecisionInstances_DecisionDefinitionKey",
                table: "DecisionInstances",
                column: "DecisionDefinitionKey");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionInstances_DecisionDefinitionKey_TenantId",
                table: "DecisionInstances",
                columns: new[] { "DecisionDefinitionKey", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionInstances_EvaluationTime",
                table: "DecisionInstances",
                column: "EvaluationTime");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionInstances_TenantId",
                table: "DecisionInstances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DmnDecisionTables_Name",
                table: "DmnDecisionTables",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionDefinitions");

            migrationBuilder.DropTable(
                name: "DecisionInstances");

            migrationBuilder.DropTable(
                name: "DmnDecisionTables");
        }
    }
}
