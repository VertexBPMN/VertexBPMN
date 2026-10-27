using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161, MA0048 // EF-generated migration layout.

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class EventStreamRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ExpiredThrough",
                table: "EventStreamSubscriptions",
                type: ActiveProvider.Contains("Npgsql", StringComparison.Ordinal) ? "bigint" : "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainedAt",
                table: "EventStreamDeliveries",
                type: ActiveProvider.Contains("Npgsql", StringComparison.Ordinal) ? "timestamp with time zone" : "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiredThrough",
                table: "EventStreamSubscriptions");

            migrationBuilder.DropColumn(
                name: "RetainedAt",
                table: "EventStreamDeliveries");
        }
    }
}
