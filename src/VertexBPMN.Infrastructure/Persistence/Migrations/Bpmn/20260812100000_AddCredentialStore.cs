using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

public partial class AddCredentialStore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Credentials",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                SecretKeysJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                ProtectedValues = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Credentials", x => x.Id));

        migrationBuilder.CreateIndex("IX_Credentials_TenantId_Name", "Credentials", new[] { "TenantId", "Name" }, unique: true);
        migrationBuilder.CreateIndex("IX_Credentials_TenantId", "Credentials", "TenantId");
        migrationBuilder.CreateIndex("IX_Credentials_LastModified", "Credentials", "LastModified");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Credentials");
    }
}
