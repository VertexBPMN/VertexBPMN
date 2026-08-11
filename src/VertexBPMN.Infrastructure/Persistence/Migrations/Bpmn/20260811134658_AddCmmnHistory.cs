using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class AddCmmnHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CmmnHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaseId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CaseFileJson = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedPlanItemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmmnHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CmmnHistory_CaseId_Timestamp",
                table: "CmmnHistory",
                columns: new[] { "CaseId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CmmnHistory");
        }
    }
}
