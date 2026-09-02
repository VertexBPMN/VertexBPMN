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
                    Id = table.Column<Guid>(nullable: false),
                    CaseId = table.Column<string>(maxLength: 255, nullable: false),
                    CaseFileJson = table.Column<string>(nullable: false),
                    CompletedPlanItemsJson = table.Column<string>(nullable: false),
                    Timestamp = table.Column<DateTime>(nullable: false)
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
