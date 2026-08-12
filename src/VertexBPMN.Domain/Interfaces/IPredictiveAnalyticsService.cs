using VertexBPMN.Domain.Entities.ML;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// ML-Based Predictive Analytics Engine
/// Olympic-level feature: Innovation Differentiators - Machine Learning Predictions
/// </summary>
public interface IPredictiveAnalyticsService
{
    Task<ProcessCompletionPrediction> PredictProcessCompletionAsync(Guid processInstanceId, string? tenantId = null);
    Task<ProcessDurationPrediction> PredictProcessDurationAsync(string processDefinitionKey, Dictionary<string, object> variables, string? tenantId = null);
    Task<ProcessBottleneckPrediction> PredictBottlenecksAsync(string processDefinitionKey, string? tenantId = null);
    Task<ProcessOptimizationSuggestion> GetOptimizationSuggestionsAsync(string processDefinitionKey, string? tenantId = null);
    Task TrainModelsAsync(string? tenantId = null);
    Task<string> ExportTrainingDataAsync(string? processDefinitionKey = null, string? tenantId = null);
}