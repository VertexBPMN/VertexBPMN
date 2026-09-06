using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class AddOAuth2FlowStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OAuth2FlowStates",
                columns: table => new
                {
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CredentialId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AuthorizationUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    TokenUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    RedirectUri = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Scopes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuth2FlowStates", x => x.State);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OAuth2FlowStates_TenantId_ExpiresAt",
                table: "OAuth2FlowStates",
                columns: new[] { "TenantId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OAuth2FlowStates");
        }
    }
}
