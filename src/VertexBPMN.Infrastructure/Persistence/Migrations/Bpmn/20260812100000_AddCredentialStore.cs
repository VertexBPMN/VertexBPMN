using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260812100000_AddCredentialStore")]
public partial class AddCredentialStore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Credentials",
            columns: table => new
            {
                Id = table.Column<string>(maxLength: 128, nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: false),
                Name = table.Column<string>(maxLength: 256, nullable: false),
                Type = table.Column<string>(maxLength: 128, nullable: false),
                Description = table.Column<string>(maxLength: 2000, nullable: true),
                SecretKeysJson = table.Column<string>(maxLength: 4000, nullable: false),
                ProtectedValues = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                LastModified = table.Column<DateTime>(nullable: false),
                LastUsedAt = table.Column<DateTime>(nullable: true)
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
