using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class AddPersistentMultiInstanceRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MultiInstanceExecutions_ProcessInstanceId_ActivityId",
                table: "MultiInstanceExecutions");

            migrationBuilder.AddColumn<string>(
                name: "LocalVariables",
                table: "Tasks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "MultiInstanceExecutionId",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MultiInstanceIndex",
                table: "Tasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionCondition",
                table: "MultiInstanceExecutions",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ElementVariable",
                table: "MultiInstanceExecutions",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemsJson",
                table: "MultiInstanceExecutions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "NextIndex",
                table: "MultiInstanceExecutions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OutputCollection",
                table: "MultiInstanceExecutions",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "MultiInstanceExecutions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "MultiInstanceExecutions",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_MultiInstanceExecutionId",
                table: "Tasks",
                column: "MultiInstanceExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_MultiInstanceExecutions_ProcessInstanceId_ActivityId_State",
                table: "MultiInstanceExecutions",
                columns: new[] { "ProcessInstanceId", "ActivityId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_MultiInstanceExecutionId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_MultiInstanceExecutions_ProcessInstanceId_ActivityId_State",
                table: "MultiInstanceExecutions");

            migrationBuilder.DropColumn(
                name: "LocalVariables",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "MultiInstanceExecutionId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "MultiInstanceIndex",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CompletionCondition",
                table: "MultiInstanceExecutions");

            migrationBuilder.DropColumn(
                name: "ElementVariable",
                table: "MultiInstanceExecutions");

            migrationBuilder.DropColumn(
                name: "ItemsJson",
                table: "MultiInstanceExecutions");

            migrationBuilder.DropColumn(
                name: "NextIndex",
                table: "MultiInstanceExecutions");

            migrationBuilder.DropColumn(
                name: "OutputCollection",
                table: "MultiInstanceExecutions");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "MultiInstanceExecutions");

            migrationBuilder.DropColumn(
                name: "State",
                table: "MultiInstanceExecutions");

            migrationBuilder.CreateIndex(
                name: "IX_MultiInstanceExecutions_ProcessInstanceId_ActivityId",
                table: "MultiInstanceExecutions",
                columns: new[] { "ProcessInstanceId", "ActivityId" });
        }
    }
}
