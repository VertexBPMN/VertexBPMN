using System.Collections.Concurrent;
using System.Diagnostics;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Engine.Security;

/// <summary>
/// Results from stress testing execution.
/// </summary>
public sealed record StressTestResult
{
	public int TotalAttempted { get; init; }
	public int CompletedSuccessfully { get; set; }
	public TimeSpan AverageParseTime { get; set; }
	public double ThroughputPerSecond { get; set; }
	public int DeadlockCount { get; set; }
	public int MemoryLeakSuspects { get; set; }
	public TimeSpan TotalExecutionTime { get; set; }
	public double MemoryUsedMB { get; set; }
	public double RetainedMemoryGrowthMB { get; set; }
	public IReadOnlyList<string> ErrorSample { get; set; } = [];
}
