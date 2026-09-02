using System.ComponentModel.DataAnnotations.Schema;

using System.Text.Json.Serialization;

namespace VertexBPMN.Domain.Model.Dmn;

/// <summary>
/// Represents a DMN decision definition.
/// Extended DecisionDefinition with parsed decision table for efficient evaluation.
/// DecisionTable is not persisted; it is generated lazily from <see cref="DmnXml"/>.
/// </summary>
public class DecisionDefinition
{
    private string _dmnXml = string.Empty;
    private DmnDecisionTable? _decisionTable; // cached parsed model

    public string Id { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// <summary>
    /// Raw DMN XML. Setting this invalidates the cached <see cref="DecisionTable"/>.
    /// </summary>
    public string DmnXml
    {
        get => _dmnXml;
        set
        {
            if (!string.Equals(_dmnXml, value, StringComparison.Ordinal))
            {
                _dmnXml = value;
                // Invalidate cached parsed table so it will be re-parsed lazily
                _decisionTable = null;
            }
        }
    }

    public string? TenantId { get; set; }

    /// <summary>
    /// Lazily parsed decision table from the current <see cref="DmnXml"/>. Not mapped/persisted.
    /// Accessing this property will parse the DMN XML once and cache the result until <see cref="DmnXml"/> changes.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public DmnDecisionTable? DecisionTable
    {
        get
        {
            if (_decisionTable == null && !string.IsNullOrWhiteSpace(_dmnXml))
            {
                try
                {
                    _decisionTable = DmnDecisionTable.Parse(_dmnXml);
                }
                catch
                {
                    // Swallow parsing errors here; caller can decide to re-parse explicitly.
                    // (Alternatively, expose validation result / throw custom exception.)
                    _decisionTable = null;
                }
            }
            return _decisionTable;
        }
        set => _decisionTable = value; // allow manual injection (e.g., pre-parsed in services)
    }

    // EF needs a parameterless constructor
    public DecisionDefinition() { }

    public DecisionDefinition(string key, string name, string dmnXml, string? tenantId, DmnDecisionTable? decisionTable = null)
    {
        Key = key;
        Name = name;
        _dmnXml = dmnXml; // direct assign to avoid double parse
        TenantId = tenantId;
        _decisionTable = decisionTable; // may be null → lazy parse on demand
        Id = BuildId(key, tenantId);
    }

    public static string BuildId(string key, string? tenantId) =>
        string.IsNullOrWhiteSpace(tenantId) ? key : $"{key}#{tenantId}";
}
