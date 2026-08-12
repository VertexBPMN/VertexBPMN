namespace VertexBPMN.Api.Migration;

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