using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

/// <summary>
/// A service-task handler that, instead of calling a connector, writes previously
/// recorded snapshot output values directly into the process variables. Used only
/// by the CLI test-runner in <c>--use-recorded-outputs</c> mode (Plan §3.6).
/// </summary>
public sealed class RecordedOutputServiceTaskHandler(
    IReadOnlyDictionary<string, object> outputs) : IServiceTaskHandler
{
    public Task ExecuteAsync(
        IDictionary<string, string> attributes,
        IDictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        foreach (var kvp in outputs)
            variables[kvp.Key] = kvp.Value;
        return Task.CompletedTask;
    }
}
