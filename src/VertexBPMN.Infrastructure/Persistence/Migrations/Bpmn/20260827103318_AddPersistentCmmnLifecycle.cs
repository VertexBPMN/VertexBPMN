using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class AddPersistentCmmnLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaseInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaseDefinitionId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CaseDefinitionKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CaseFileJson = table.Column<string>(type: "TEXT", nullable: false),
                    PlanItemStatesJson = table.Column<string>(type: "TEXT", nullable: false),
                    DiscretionaryItemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseInstances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseInstances_CaseDefinitionId",
                table: "CaseInstances",
                column: "CaseDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseInstances_TenantId_CaseDefinitionKey_State",
                table: "CaseInstances",
                columns: new[] { "TenantId", "CaseDefinitionKey", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseInstances");
        }
    }
}
