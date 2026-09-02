using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260818090000_AddConnectorTemplates")]
public partial class AddConnectorTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ConnectorTemplates",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: false),
                Name = table.Column<string>(maxLength: 256, nullable: false),
                Category = table.Column<string>(maxLength: 128, nullable: false),
                AppliesToJson = table.Column<string>(nullable: false),
                Runtime = table.Column<string>(maxLength: 128, nullable: false),
                Icon = table.Column<string>(maxLength: 256, nullable: true),
                PropertiesJson = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                LastModified = table.Column<DateTime>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ConnectorTemplates", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ConnectorTemplates_TenantId",
            table: "ConnectorTemplates",
            column: "TenantId");
        migrationBuilder.CreateIndex(
            name: "IX_ConnectorTemplates_TenantId_Name",
            table: "ConnectorTemplates",
            columns: new[] { "TenantId", "Name" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "ConnectorTemplates");
}
