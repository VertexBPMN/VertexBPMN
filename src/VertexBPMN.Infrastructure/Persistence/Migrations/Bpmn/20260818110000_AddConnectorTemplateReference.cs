using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable
namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260818110000_AddConnectorTemplateReference")]
public partial class AddConnectorTemplateReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "TemplateId", table: "Connectors", type: "TEXT", maxLength: 128, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Connectors_TemplateId", table: "Connectors", column: "TemplateId");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Connectors_TemplateId", table: "Connectors");
        migrationBuilder.DropColumn(name: "TemplateId", table: "Connectors");
    }
}
