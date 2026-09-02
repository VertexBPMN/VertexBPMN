using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence.Services;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.ProcessMiningEvents;

[DbContext(typeof(ProcessMiningEventDbContext))]
[Migration("20260827150001_AddSourceEventId")]
public partial class AddSourceEventId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SourceEventId",
            table: "Events",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Events_SourceEventId",
            table: "Events",
            column: "SourceEventId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Events_SourceEventId",
            table: "Events");

        migrationBuilder.DropColumn(
            name: "SourceEventId",
            table: "Events");
    }
}
