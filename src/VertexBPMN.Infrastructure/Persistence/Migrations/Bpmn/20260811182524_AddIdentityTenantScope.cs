using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class AddIdentityTenantScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "IdentityGroupMemberships",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "IdentityAuthorizations",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityGroupMemberships_TenantId",
                table: "IdentityGroupMemberships",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAuthorizations_TenantId",
                table: "IdentityAuthorizations",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdentityGroupMemberships_TenantId",
                table: "IdentityGroupMemberships");

            migrationBuilder.DropIndex(
                name: "IX_IdentityAuthorizations_TenantId",
                table: "IdentityAuthorizations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "IdentityGroupMemberships");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "IdentityAuthorizations");
        }
    }
}
