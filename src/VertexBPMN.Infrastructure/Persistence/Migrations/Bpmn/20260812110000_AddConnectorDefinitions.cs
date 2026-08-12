using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

public partial class AddConnectorDefinitions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Connectors",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                Endpoint = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                CredentialId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Connectors", x => x.Id));

        migrationBuilder.CreateIndex("IX_Connectors_TenantId_Name", "Connectors", new[] { "TenantId", "Name" }, unique: true);
        migrationBuilder.CreateIndex("IX_Connectors_TenantId", "Connectors", "TenantId");
        migrationBuilder.CreateIndex("IX_Connectors_CredentialId", "Connectors", "CredentialId");
        migrationBuilder.CreateIndex("IX_Connectors_LastModified", "Connectors", "LastModified");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "Connectors");
}
