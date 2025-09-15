using VertexBPMN.Domain.Entities.Modeling;

namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Represents a DMN decision definition.
/// Extended DecisionDefinition with parsed decision table for efficient evaluation.
/// </summary>
public record DecisionDefinition(string Key, string Name, string DmnXml, string? TenantId, DmnDecisionTable? DecisionTable = null)
{
    /// <summary>
    /// Backward compatibility constructor without DecisionTable.
    /// </summary>
    public DecisionDefinition(string Key, string Name, string DmnXml, string? TenantId)
        : this(Key, Name, DmnXml, TenantId, null) { }
}

