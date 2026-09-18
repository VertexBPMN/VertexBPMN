using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Studio.Migrations.OidcSession
{
    /// <inheritdoc />
    public partial class AddOidcSessionStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Providerneutral: keine expliziten SQL-Typen (wie bei DependencyRegistry),
            // damit dieselbe Migration lokal auf SQLite und in Azure auf PostgreSQL läuft.
            migrationBuilder.CreateTable(
                name: "OidcSessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(maxLength: 64, nullable: false),
                    Subject = table.Column<string>(maxLength: 256, nullable: false),
                    ProtectedPayload = table.Column<byte[]>(nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(nullable: false),
                    Revision = table.Column<long>(nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OidcSessions", x => x.SessionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OidcSessions_ExpiresAtUtc",
                table: "OidcSessions",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OidcSessions");
        }
    }
}
