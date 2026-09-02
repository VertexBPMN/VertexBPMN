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

    public Task<ProcessCompletionPrediction> PredictProcessCompletionAsync(Guid processInstanceId, string? tenantId = null) =>
        Task.FromException<ProcessCompletionPrediction>(Unavailable());

    public Task<ProcessDurationPrediction> PredictProcessDurationAsync(string processDefinitionKey, Dictionary<string, object> variables, string? tenantId = null) =>
        Task.FromException<ProcessDurationPrediction>(Unavailable());

    public Task<ProcessBottleneckPrediction> PredictBottlenecksAsync(string processDefinitionKey, string? tenantId = null) =>
        Task.FromException<ProcessBottleneckPrediction>(Unavailable());

    public Task<ProcessOptimizationSuggestion> GetOptimizationSuggestionsAsync(string processDefinitionKey, string? tenantId = null) =>
        Task.FromException<ProcessOptimizationSuggestion>(Unavailable());

    public Task TrainModelsAsync(string? tenantId = null) => Task.FromException(Unavailable());

    public Task<string> ExportTrainingDataAsync(string? processDefinitionKey = null, string? tenantId = null) =>
        Task.FromException<string>(Unavailable());
}