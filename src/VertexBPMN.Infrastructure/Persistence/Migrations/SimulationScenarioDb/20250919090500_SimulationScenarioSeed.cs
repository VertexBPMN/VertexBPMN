using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Persistence.Migrations.SimulationScenarioDb
{
    public partial class SimulationScenarioSeed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Scenarios",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    BpmnXml = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessDefinitionId = table.Column<string>(type: "TEXT", nullable: false),
                    MaxSteps = table.Column<int>(type: "INTEGER", nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenarios", x => x.Id);
                });

            // Seed sample scenario (matches OnModelCreating id)
            migrationBuilder.InsertData(
                table: "Scenarios",
                columns: new[] { "Id", "BpmnXml", "Name", "Description", "ProcessDefinitionId", "MaxSteps", "TenantId" },
                values: new object[] { "sim-sample-1", null, "Throughput Test", "Ein einfacher Simulationstest", "22222222-2222-2222-2222-222222222222", 100, "tenant-default" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Scenarios");
        }
    }
}
