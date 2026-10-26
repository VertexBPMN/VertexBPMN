using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable
#pragma warning disable MA0048 // The migration file name is the canonical identifier.

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

/// <inheritdoc />
[DbContext(typeof(BpmnDbContext))]
[Migration("20261026152000_LC08ExternalTaskEffectReceipt")]
public class LC08ExternalTaskEffectReceipt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExternalTaskEffectReceipts",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                JobId = table.Column<string>(type: "TEXT", nullable: false),
                ActivityExecutionId = table.Column<string>(type: "TEXT", nullable: false),
                EffectKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                EffectName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                CompletionId = table.Column<string>(type: "TEXT", nullable: false),
                ResultHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExternalTaskEffectReceipts", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExternalTaskEffectReceipts_ExternalTaskJobs_JobId",
                    column: x => x.JobId,
                    principalTable: "ExternalTaskJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_ExternalTaskEffectReceipts_Name", "\"EffectName\" IN ('primary')");
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExternalTaskEffectReceipts_EffectKey",
            table: "ExternalTaskEffectReceipts",
            column: "EffectKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExternalTaskEffectReceipts_JobId",
            table: "ExternalTaskEffectReceipts",
            column: "JobId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ExternalTaskEffectReceipts");
    }
}
