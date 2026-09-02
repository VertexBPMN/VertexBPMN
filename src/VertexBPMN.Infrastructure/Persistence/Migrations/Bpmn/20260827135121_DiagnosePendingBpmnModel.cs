using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class DiagnosePendingBpmnModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The same model change is already applied by
            // 20260827130000_AddReusableEventSubscriptionKey.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op; see Up.
        }
    }
}
