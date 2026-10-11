using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Performance metric data structure
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type PerformanceMetric retained for exposed HTTP action signatures and controller activation (PerformanceMetric).")]
public record PerformanceMetric
{
	public DateTime Timestamp { get; init; }
	public double CpuUsage { get; init; }
	public double MemoryUsage { get; init; }
	public int RequestCount { get; init; }
	public double AvgResponseTime { get; init; }
}
