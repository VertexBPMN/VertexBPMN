using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260820003000_AddCaseDefinitions")]
public partial class AddCaseDefinitions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CaseDefinitions",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: false),
                Key = table.Column<string>(maxLength: 128, nullable: false),
                Name = table.Column<string>(maxLength: 256, nullable: false),
                CmmnXml = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                LastModified = table.Column<DateTime>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CaseDefinitions", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_CaseDefinitions_TenantId_Key", table: "CaseDefinitions", columns: new[] { "TenantId", "Key" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "CaseDefinitions");
}
