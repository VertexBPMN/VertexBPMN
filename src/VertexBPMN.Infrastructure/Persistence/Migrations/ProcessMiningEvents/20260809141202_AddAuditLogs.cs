using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.ProcessMiningEvents;

    /// <inheritdoc />
    [SuppressMessage("Meziantou.Analyzer", "MA0048", Justification = "EF migration snapshot / generated code.")]
    public partial class AddAuditLogs : Migration
    {
        private static readonly string[] TenantTimestampColumns = new[] { "TenantId", "Timestamp" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTimeOffset>(nullable: false),
                    Action = table.Column<string>(maxLength: 200, nullable: false),
                    Resource = table.Column<string>(maxLength: 300, nullable: true),
                    ResourceId = table.Column<string>(maxLength: 200, nullable: true),
                    UserId = table.Column<string>(maxLength: 200, nullable: true),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(maxLength: 128, nullable: true),
                    StatusCode = table.Column<int>(nullable: false),
                    DetailsJson = table.Column<string>(maxLength: 8000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_Timestamp",
                table: "AuditLogs",
                columns: TenantTimestampColumns);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
