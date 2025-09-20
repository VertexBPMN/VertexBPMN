using System.ComponentModel.DataAnnotations.Schema;
using VertexBPMN.Domain.Entities.Modeling;

namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Represents a DMN decision definition.
/// Extended DecisionDefinition with parsed decision table for efficient evaluation.
/// </summary>
public class DecisionDefinition
{
    public string Id { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string DmnXml { get; set; } = default!;
    public string? TenantId { get; set; }
    public DmnDecisionTable? DecisionTable { get; set; }

    // EF needs a parameterless constructor
    public DecisionDefinition() { }

    public DecisionDefinition(string key, string name, string dmnXml, string? tenantId, DmnDecisionTable? decisionTable = null)
    {
        Key = key;
        Name = name;
        DmnXml = dmnXml;
        TenantId = tenantId;
        DecisionTable = decisionTable;
        Id = BuildId(key, tenantId);
    }

    public static string BuildId(string key, string? tenantId) =>
        string.IsNullOrWhiteSpace(tenantId) ? key : $"{key}#{tenantId}";
}
