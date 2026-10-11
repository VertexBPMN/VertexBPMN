namespace VertexBPMN.Api.Migration;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type MigrationStatus retained for exposed HTTP action signatures and controller activation (MigrationStatus).")]
public enum MigrationStatus
{
    NotFound,
    Planned,
    InProgress,
    Completed,
    Failed,
    RollingBack,
    RolledBack
}
