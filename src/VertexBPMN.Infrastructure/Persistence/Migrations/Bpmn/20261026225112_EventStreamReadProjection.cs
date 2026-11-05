using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161, MA0048, CA1861 // EF-generated migration layout and metadata arrays.

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class EventStreamReadProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var guidType = ActiveProvider.Contains("Npgsql", StringComparison.Ordinal) ? "uuid" : "TEXT";
            var idType = ActiveProvider.Contains("Npgsql", StringComparison.Ordinal) ? "bigint" : "INTEGER";
            migrationBuilder.CreateTable(
                name: "EventStreamSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStreamSubscriptions", x => x.Id);
                    table.UniqueConstraint("AK_EventStreamSubscriptions_Id_TenantId", x => new { x.Id, x.TenantId });
                });

            migrationBuilder.CreateTable(
                name: "EventStreamDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: idType, nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriptionId = table.Column<Guid>(type: guidType, nullable: false),
                    SourceEventId = table.Column<Guid>(type: guidType, nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStreamDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventStreamDeliveries_EventStreamSubscriptions_SubscriptionId_TenantId",
                        columns: x => new { x.SubscriptionId, x.TenantId },
                        principalTable: "EventStreamSubscriptions",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventStreamDeliveries_SubscriptionId_SourceEventId",
                table: "EventStreamDeliveries",
                columns: new[] { "SubscriptionId", "SourceEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventStreamDeliveries_SubscriptionId_TenantId",
                table: "EventStreamDeliveries",
                columns: new[] { "SubscriptionId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventStreamDeliveries");

            migrationBuilder.DropTable(
                name: "EventStreamSubscriptions");
        }
    }
}
