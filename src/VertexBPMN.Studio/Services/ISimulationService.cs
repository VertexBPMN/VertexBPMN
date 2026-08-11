using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface ISimulationService
{
    Task<JsonElement> SimulateAsync(
        string bpmnXml,
        string processDefinitionId,
        IDictionary<string, object?>? variables = null,
        int? maxSteps = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<JsonElement> GetSummaryAsync(JsonElement simulationResult, CancellationToken cancellationToken = default);
    Task<JsonElement> GetStepBreakdownAsync(JsonElement simulationResult, CancellationToken cancellationToken = default);
    Task<JsonElement> GetVariableTraceAsync(JsonElement simulationResult, string variableName, CancellationToken cancellationToken = default);
    Task<JsonElement> CompareAsync(JsonElement resultA, JsonElement resultB, CancellationToken cancellationToken = default);
}
