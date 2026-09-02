//

//namespace VertexBPMN.Domain.Entities;

///// <summary>
///// Represents a DMN decision definition.
///// </summary>
//public record DecisionDefinition(
//    string Key, 
//    string Name, 
//    string DmnXml, 
//    string? TenantId, 
//    DmnDecisionTable? DecisionTable = null)
//{
//    /// <summary>
//    /// Backward compatibility constructor without DecisionTable.
//    /// </summary>
//    public DecisionDefinition(string Key, string Name, string DmnXml, string? TenantId) 
//        : this(Key, Name, DmnXml, TenantId, null) { }
//}

///// <summary>
///// Represents the result of a decision evaluation.
///// </summary>
//public record DecisionResult(IDictionary<string, object> Variables)
//{
//    /// <summary>
//    /// Gets the output values from the decision evaluation.
//    /// Alias for Variables to maintain API compatibility.
//    /// </summary>
//    public IDictionary<string, object> Outputs => Variables;
//}

///// <summary>
///// Represents a decision evaluation instance for audit and history.
///// </summary>
//public record DecisionInstance(
//    string Id,
//    string DecisionDefinitionKey,
//    string? TenantId,
//    DateTime EvaluationTime,
//    IDictionary<string, object> InputVariables,
//    IDictionary<string, object> OutputVariables,
//    bool Failed = false,
//    string? ErrorMessage = null);