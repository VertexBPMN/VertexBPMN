namespace VertexBPMN.Domain.Model.Security;

/// <summary>
/// Results from fuzz testing execution.
/// </summary>
public sealed record FuzzTestResult
{
    public int TotalExecutions { get; set; }
    public int SuccessfulParses { get; set; }
    public int HandledFailures { get; set; }
    public int CrashCount { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}