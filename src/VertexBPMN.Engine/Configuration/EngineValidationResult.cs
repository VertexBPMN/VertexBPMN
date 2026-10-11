using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Engine.Configuration;

/// <summary>
/// Result of engine capability validation
/// </summary>
/// <param name="IsValid">Whether the engine meets all requirements</param>
/// <param name="Issues">List of capability issues</param>
/// <param name="Recommendations">List of recommendations to resolve issues</param>
/// <param name="EngineType">Detected engine type</param>
public record EngineValidationResult(
	bool IsValid,
	List<string> Issues,
	List<string> Recommendations,
	ProcessEngineType EngineType
);
