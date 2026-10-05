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
    public IReadOnlyList<string> AllowedHosts { get; init; } = [];
    public string WorkBranchPrefix { get; init; } = "vertex/";
    public SourceControlLimits Limits { get; init; } = new();
}

public sealed class SourceControlLimits
{
    public long MaxRepositoryBytes { get; init; } = 256L * 1024 * 1024;
    public int MaxModelBytes { get; init; } = 2 * 1024 * 1024;
    public int MaxModelFiles { get; init; } = 1_000;
    public int MaxDiffBytes { get; init; } = 1024 * 1024;
    public int MaxPageSize { get; init; } = 100;
    public int MaxCommitFiles { get; init; } = 20;
    public int MaxCommitMessageCharacters { get; init; } = 2_000;
    public int MaxConcurrentJobsPerTenant { get; init; } = 2;
    public int MaxConcurrentJobsTotal { get; init; } = 8;
    public long MaxWorkspaceBytesTotal { get; init; } = 5L * 1024 * 1024 * 1024;
    public TimeSpan ReadTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan WriteTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan SessionIdleRetention { get; init; } = TimeSpan.FromHours(24);
    public TimeSpan CompletedJobRetention { get; init; } = TimeSpan.FromDays(30);
}
