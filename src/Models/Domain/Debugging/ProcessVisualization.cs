using System;
using System.Collections.Generic;

namespace VertexBPMN.Domain.Debugging;

public class ProcessVisualization
{
    public Guid ProcessInstanceId { get; set; }
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string BpmnXml { get; set; } = string.Empty;
    public List<VisualToken> ActiveTokens { get; set; } = new();
    public List<VisualActivity> CompletedActivities { get; set; } = new();
    public VisualizationMetrics Metrics { get; set; } = new();
}