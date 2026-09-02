using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class AddPersistentIdentityReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdentityAuthorizations",
                columns: table => new
                {
                    Id = table.Column<string>(maxLength: 128, nullable: false),
                    UserId = table.Column<string>(maxLength: 128, nullable: false),
                    GroupId = table.Column<string>(maxLength: 128, nullable: false),
                    Resource = table.Column<string>(maxLength: 512, nullable: false),
                    Permissions = table.Column<string>(maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityAuthorizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdentityGroupMemberships",
                columns: table => new
                {
                    GroupId = table.Column<string>(maxLength: 128, nullable: false),
                    UserId = table.Column<string>(maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityGroupMemberships", x => new { x.GroupId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "IdentityGroups",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    Name = table.Column<string>(maxLength: 256, nullable: false),
                    Type = table.Column<string>(maxLength: 128, nullable: false),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAuthorizations_UserId_GroupId_Resource",
                table: "IdentityAuthorizations",
                columns: new[] { "UserId", "GroupId", "Resource" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityGroupMemberships_UserId",
                table: "IdentityGroupMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityGroups_TenantId_Name",
                table: "IdentityGroups",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentityAuthorizations");

            migrationBuilder.DropTable(
                name: "IdentityGroupMemberships");

            migrationBuilder.DropTable(
                name: "IdentityGroups");
        }
    }
}
