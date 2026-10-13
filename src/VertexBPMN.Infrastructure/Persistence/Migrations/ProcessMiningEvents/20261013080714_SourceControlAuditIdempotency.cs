using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.ProcessMiningEvents;
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Meziantou.Analyzer", "MA0048", Justification = "EF migration filenames include their generated timestamp.")]
    public partial class SourceControlAuditIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceControlEventId",
                table: "AuditLogs",
                nullable: true);

            migrationBuilder.AlterColumn<string>(name: "UserId", table: "AuditLogs", maxLength: 512, nullable: true,
                oldClrType: typeof(string), oldMaxLength: 200, oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SourceControlEventId",
                table: "AuditLogs",
                column: "SourceControlEventId",
                unique: true,
                filter: "\"SourceControlEventId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(name: "UserId", table: "AuditLogs", maxLength: 200, nullable: true,
                oldClrType: typeof(string), oldMaxLength: 512, oldNullable: true);
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_SourceControlEventId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "SourceControlEventId",
                table: "AuditLogs");
        }
    }
