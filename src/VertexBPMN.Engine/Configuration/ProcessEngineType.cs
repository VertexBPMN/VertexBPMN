using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Engine.Configuration;

/// <summary>
/// Engine types supported by the factory
/// </summary>
public enum ProcessEngineType
{
	/// <summary>
	/// Simple, single-node engine with in-memory BPMN, CMMN and DMN execution
	/// - ✅ BPMN execution
	/// - ✅ CMMN support
	/// - ✅ DMN registry
	/// - ❌ Distribution
	/// - ❌ Worker management
	/// </summary>
	Simple,

	/// <summary>
	/// Distributed, enterprise-grade engine with the same domain features plus server capabilities
	/// - ✅ BPMN execution
	/// - ✅ CMMN case management
	/// - ✅ DMN decision support
	/// - ✅ Distributed processing
	/// - ✅ Worker management
	/// - ✅ AI-enhanced features
	/// </summary>
	Distributed
}
