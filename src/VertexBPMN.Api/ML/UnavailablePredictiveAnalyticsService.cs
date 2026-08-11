using VertexBPMN.Domain.Entities.ML;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.ML;

/// <summary>
/// Explicitly reports that predictive analytics is unavailable until it is backed by persisted execution data.
/// </summary>
public sealed class UnavailablePredictiveAnalyticsService : IPredictiveAnalyticsService
{
    private static NotSupportedException Unavailable() =>
        new("Predictive analytics requires a configured historical-data pipeline and is not available yet.");

    public Task<ProcessCompletionPrediction> PredictProcessCompletionAsync(Guid processInstanceId) =>
        Task.FromException<ProcessCompletionPrediction>(Unavailable());

    public Task<ProcessDurationPrediction> PredictProcessDurationAsync(string processDefinitionKey, Dictionary<string, object> variables) =>
        Task.FromException<ProcessDurationPrediction>(Unavailable());

    public Task<ProcessBottleneckPrediction> PredictBottlenecksAsync(string processDefinitionKey) =>
        Task.FromException<ProcessBottleneckPrediction>(Unavailable());

    public Task<ProcessOptimizationSuggestion> GetOptimizationSuggestionsAsync(string processDefinitionKey) =>
        Task.FromException<ProcessOptimizationSuggestion>(Unavailable());

    public Task TrainModelsAsync() => Task.FromException(Unavailable());
}