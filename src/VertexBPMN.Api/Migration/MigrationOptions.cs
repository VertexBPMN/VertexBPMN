namespace VertexBPMN.Api.Migration;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type MigrationOptions retained for exposed HTTP action signatures and controller activation (MigrationOptions).")]
public class MigrationOptions
{
    public bool CreateBackups { get; set; } = true;
    public bool ValidateBeforeMigration { get; set; } = true;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "S6964", Justification = "Optional migration option: omission intentionally means false. Requiring it would break existing migration requests; safety validation remains in the migration service.")]
    public bool AllowPartialMigration { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(1);
    public int MaxConcurrentMigrations { get; set; } = 5;
}
