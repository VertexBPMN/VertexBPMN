using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260827150000_AddRuntimeAnalyticsProjection")]
public partial class AddRuntimeAnalyticsProjection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "AnalyticsProjectedAt",
            table: "RuntimeOutbox",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AnalyticsProjectedAt",
            table: "RuntimeOutbox");
    }
}
