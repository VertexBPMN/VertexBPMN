namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>
/// Application binds and validates configuration on startup; disabled by default.
/// G03/G04 must still enforce these quotas at the actual storage/transport boundaries.
/// </summary>
public sealed class SourceControlOptions
{
    public const string SectionName = "SourceControl";
    public bool Enabled { get; init; }
    public string? GitExecutablePath { get; init; }
    public string? AuthHelperExecutablePath { get; init; }
    public string? WorkspaceRoot { get; init; }
    public string? IdentityCredentialReference { get; init; }
    public string? IdentityAuthority { get; set; }
    public IReadOnlyList<string> AllowedHosts { get; init; } = [];
    public string WorkBranchPrefix { get; init; } = "vertex/";
    public SourceControlLimits Limits { get; init; } = new();
}
