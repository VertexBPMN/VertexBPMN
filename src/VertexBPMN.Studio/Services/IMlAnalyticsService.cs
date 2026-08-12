using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IMlAnalyticsService
{
    Task<JsonElement> PredictCompletionAsync(Guid processInstanceId, CancellationToken cancellationToken = default);
    Task<JsonElement> PredictDurationAsync(string processDefinitionKey, IDictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task<JsonElement> PredictBottlenecksAsync(string processDefinitionKey, CancellationToken cancellationToken = default);
    Task<JsonElement> GetOptimizationSuggestionsAsync(string processDefinitionKey, CancellationToken cancellationToken = default);
}
