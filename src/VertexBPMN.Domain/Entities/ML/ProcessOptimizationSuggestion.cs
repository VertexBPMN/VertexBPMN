using System;
using System.Collections.Generic;

namespace VertexBPMN.Domain.ML;

public class ProcessOptimizationSuggestion
{
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public float OverallScore { get; set; }
    public List<OptimizationAction> Suggestions { get; set; } = new();
    public float ModelConfidence { get; set; }
    public DateTime NextReviewDate { get; set; }
}