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
            migrationBuilder.CreateTable(
                name: "FormDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    TenantId = table.Column<string>(maxLength: 64, nullable: false),
                    Key = table.Column<string>(maxLength: 128, nullable: false),
                    Name = table.Column<string>(maxLength: 256, nullable: false),
                    Schema = table.Column<string>(nullable: false),
                    Version = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    LastModified = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDefinitions", x => x.Id);
                });

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
                name: "FormDefinitions");
        }
    }
}
