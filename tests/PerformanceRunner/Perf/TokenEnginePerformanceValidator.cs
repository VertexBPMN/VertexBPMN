using System.Diagnostics;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;

namespace PerformanceRunner.Perf;

/// <summary>
/// Simple performance validation runner for the token engines
/// </summary>
public class TokenEnginePerformanceValidator
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

        // Test ProcessEngine
        try
        {
            var processEngine = new ProcessEngine();
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < 1000; i++)
            {
                var trace = processEngine.Execute(model);
                if (trace.Count == 0)
                {
                    results.Add("ERROR: ProcessEngine returned empty trace");
                    break;
                }
            }
            
            sw.Stop();
            results.Add($"? ProcessEngine: 1,000 executions in {sw.ElapsedMilliseconds}ms");
            
            if (sw.ElapsedMilliseconds > 5000)
            {
                results.Add($"? ProcessEngine performance warning: {sw.ElapsedMilliseconds}ms > 5000ms threshold");
            }
        }
        catch (Exception ex)
        {
            results.Add($"? ProcessEngine failed: {ex.Message}");
        }

        try
        {
            var processEngine = new ProcessEngine();
            var trace = processEngine.Execute(model);
            
            results.Add($"? Basic functionality test: {trace.Count} trace entries");
            
            if (trace.Any(t => t.Contains("StartEvent")))
            {
                results.Add("? Start event processing confirmed");
            }
            
            if (trace.Any(t => t.Contains("EndEvent")))
            {
                results.Add("? End event processing confirmed");
            }
        }
        catch (Exception ex)
        {
            results.Add($"? Basic functionality test failed: {ex.Message}");
        }

        return string.Join(Environment.NewLine, results);
    }
}