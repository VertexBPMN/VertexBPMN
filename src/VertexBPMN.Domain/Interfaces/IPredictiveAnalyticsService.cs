using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VertexBPMN.Domain.ML;

namespace VertexBPMN.Domain.Contracts;

/// <summary>
/// ML-Based Predictive Analytics Engine
/// Olympic-level feature: Innovation Differentiators - Machine Learning Predictions
/// </summary>
public interface IPredictiveAnalyticsService
{
    Task<ProcessCompletionPrediction> PredictProcessCompletionAsync(Guid processInstanceId);
    Task<ProcessDurationPrediction> PredictProcessDurationAsync(string processDefinitionKey, Dictionary<string, object> variables);
    Task<ProcessBottleneckPrediction> PredictBottlenecksAsync(string processDefinitionKey);
    Task<ProcessOptimizationSuggestion> GetOptimizationSuggestionsAsync(string processDefinitionKey);
    Task TrainModelsAsync();
}