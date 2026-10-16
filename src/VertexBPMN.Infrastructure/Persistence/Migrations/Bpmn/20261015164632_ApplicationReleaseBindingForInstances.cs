using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable MA0048 // EF migration filenames.

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;
    /// <inheritdoc />
    public partial class ApplicationReleaseBindingForInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationReleaseId",
                table: "ProcessInstances",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationReleaseId",
                table: "Jobs",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"ProcessInstances\" DROP COLUMN \"ApplicationReleaseId\";");
            migrationBuilder.Sql("ALTER TABLE \"Jobs\" DROP COLUMN \"ApplicationReleaseId\";");
        }
    }
