using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Application;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;

namespace PerformanceRunner.Perf;

/// <summary>
/// Simple performance validation runner for the token engines
/// </summary>
internal static class DistributedProcessEnginePerformanceValidator
{
	public static async Task<string> RunValidationAsync()
	{
		var results = new List<string>();

		// Create a simple test model
		var model = new BpmnModel(
			"TestProcess",
			"Performance Test",
			new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
			new List<BpmnGateway>(),
			new List<BpmnSubprocess>(),
			new List<BpmnSequenceFlow> { new("flow1", "start1", "end1") },
			new List<BpmnTask>()
		);


		// Test ProposalTokenEngine
		try
		{
			var logger = new Mock<ILogger<ProposalTokenEngine>>();
			var registry = new ServiceTaskRegistry();
			using var proposalEngine = new ProposalTokenEngine(logger.Object, registry);
			var sw = Stopwatch.StartNew();

			for (var i = 0; i < 1000; i++)
			{
				var trace = await proposalEngine.ExecuteAsync(model);
				if (trace.Count == 0)
				{
					results.Add("ERROR: ProposalTokenEngine returned empty trace");
					break;
				}
			}

			sw.Stop();
			results.Add($"? ProposalTokenEngine: 1,000 executions in {sw.ElapsedMilliseconds}ms");

			if (sw.ElapsedMilliseconds > 10000)
			{
				results.Add($"? ProposalTokenEngine performance warning: {sw.ElapsedMilliseconds}ms > 10000ms threshold");
			}
		}
		catch (Exception ex)
		{
			results.Add($"? ProposalTokenEngine failed: {ex.Message}");
		}

		await ValidateBasicFunctionalityAsync(model, results);

		return string.Join(Environment.NewLine, results);
	}
	private static async Task ValidateBasicFunctionalityAsync(BpmnModel model, List<string> results)
	{
		// Test basic functionality
		try
		{
			var processEngine = new ProcessEngine();
			var trace = await processEngine.ExecuteAsync(model, CancellationToken.None);

			results.Add($"? Basic functionality test: {trace.Count} trace entries");

			if (trace.Any(t => t.Contains("StartEvent", StringComparison.Ordinal)))
			{
				results.Add("? Start event processing confirmed");
			}

			if (trace.Any(t => t.Contains("EndEvent", StringComparison.Ordinal)))
			{
				results.Add("? End event processing confirmed");
			}
		}
		catch (Exception ex)
		{
			results.Add($"? Basic functionality test failed: {ex.Message}");
		}

	}
}
