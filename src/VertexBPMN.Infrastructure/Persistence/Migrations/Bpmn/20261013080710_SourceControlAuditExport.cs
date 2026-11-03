using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Meziantou.Analyzer", "MA0048", Justification = "EF migration filenames include their generated timestamp.")]
    public partial class SourceControlAuditExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AuditExportedUtcTicks",
                table: "SourceControlOperations",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AuditPending",
                table: "SourceControlOperations",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "AuditRetryAfterUtcTicks",
                table: "SourceControlOperations",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"SourceControlOperations\" DROP COLUMN \"AuditExportedUtcTicks\";");

            migrationBuilder.Sql("ALTER TABLE \"SourceControlOperations\" DROP COLUMN \"AuditPending\";");

            migrationBuilder.Sql("ALTER TABLE \"SourceControlOperations\" DROP COLUMN \"AuditRetryAfterUtcTicks\";");
        }
    }
