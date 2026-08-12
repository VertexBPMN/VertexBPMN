using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IMlAnalyticsService
{
    Task<JsonElement> PredictCompletionAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<JsonElement> PredictDurationAsync(string processDefinitionKey, IDictionary<string, object?>? variables = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<JsonElement> PredictBottlenecksAsync(string processDefinitionKey, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<JsonElement> GetOptimizationSuggestionsAsync(string processDefinitionKey, string? tenantId = null, CancellationToken cancellationToken = default);
    Task TrainModelsAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<byte[]> ExportTrainingDataAsync(string? processDefinitionKey = null, string? tenantId = null, CancellationToken cancellationToken = default);
}
