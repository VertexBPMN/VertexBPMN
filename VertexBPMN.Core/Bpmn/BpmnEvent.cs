namespace VertexBPMN.Core.Bpmn;

/// <summary>
/// Represents a BPMN event in the process model.
/// </summary>
public record BpmnEvent(
    string Id, 
    string Type, 
    string? AttachedToRef = null, 
    bool IsCompensation = false, 
    bool CancelActivity = true, 
    string? EventDefinitionType = null)
{
    // Nachrichteneigenschaften
    public string? CorrelationKey { get; init; } // Used for message events

    // Timer-Eigenschaften
    public DateTime? TimerDueDate { get; init; } // Used for timer events

    // Signal-Eigenschaften
    public string? SignalName { get; init; } // Used for signal events

    // Fehler-Eigenschaften
    public string? ErrorCode { get; init; } // Used for error events

    // Bedingungseigenschaften
    public string? ConditionExpression { get; init; } // Used for condition events

    /// <summary>
    /// Additional attributes for extensibility.
    /// </summary>
    public IDictionary<string, object> Attributes { get; init; } = new Dictionary<string, object>();
}