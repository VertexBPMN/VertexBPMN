namespace VertexBPMN.Api.Migration;

public class MigrationOptions
{
    public bool CreateBackups { get; set; } = true;
    public bool ValidateBeforeMigration { get; set; } = true;
    public bool AllowPartialMigration { get; set; } = false;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(1);
    public int MaxConcurrentMigrations { get; set; } = 5;
}