using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260812110000_AddConnectorDefinitions")]
public partial class AddConnectorDefinitions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Connectors",
            columns: table => new
            {
                Id = table.Column<string>(maxLength: 128, nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: false),
                Name = table.Column<string>(maxLength: 256, nullable: false),
                Type = table.Column<string>(maxLength: 128, nullable: false),
                Description = table.Column<string>(maxLength: 2000, nullable: true),
                Endpoint = table.Column<string>(maxLength: 2048, nullable: true),
                CredentialId = table.Column<string>(maxLength: 128, nullable: true),
                Enabled = table.Column<bool>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                LastModified = table.Column<DateTime>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Connectors", x => x.Id));

        migrationBuilder.CreateIndex("IX_Connectors_TenantId_Name", "Connectors", new[] { "TenantId", "Name" }, unique: true);
        migrationBuilder.CreateIndex("IX_Connectors_TenantId", "Connectors", "TenantId");
        migrationBuilder.CreateIndex("IX_Connectors_CredentialId", "Connectors", "CredentialId");
        migrationBuilder.CreateIndex("IX_Connectors_LastModified", "Connectors", "LastModified");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "Connectors");
}
