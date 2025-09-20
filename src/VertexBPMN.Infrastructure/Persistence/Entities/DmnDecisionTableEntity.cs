using System.Text.Json;
using VertexBPMN.Domain.Entities.Modeling;

namespace VertexBPMN.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistence entity for a DMN decision table. Keeps a serialized snapshot of inputs, outputs and rules.
/// Domain layer still reconstructs a runtime <see cref="DmnDecisionTable"/> when needed.
/// </summary>
public class DmnDecisionTableEntity
{
    public string Id { get; set; } = default!;              // decision key (or composite)
    public string Key { get; set; } = default!;             // business key
    public string Name { get; set; } = default!;
    public string HitPolicy { get; set; } = "UNIQUE";
    public string InputsJson { get; set; } = "[]";          // serialized List<DmnInput>
    public string OutputsJson { get; set; } = "[]";         // serialized List<DmnOutput>
    public string RulesJson { get; set; } = "[]";           // serialized List<DmnRule>
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static DmnDecisionTableEntity FromDomain(string id, string? tenantId, DmnDecisionTable table)
        => new()
        {
            Id = id,
            Key = table.Key,
            Name = table.Name,
            HitPolicy = table.HitPolicy,
            InputsJson = JsonSerializer.Serialize(table.Inputs, JsonOpts),
            OutputsJson = JsonSerializer.Serialize(table.Outputs, JsonOpts),
            RulesJson = JsonSerializer.Serialize(table.Rules, JsonOpts),
            TenantId = tenantId
        };

    public DmnDecisionTable ToDomain()
    {
        var inputs = JsonSerializer.Deserialize<List<DmnInput>>(InputsJson, JsonOpts) ?? new();
        var outputs = JsonSerializer.Deserialize<List<DmnOutput>>(OutputsJson, JsonOpts) ?? new();
        var rules = JsonSerializer.Deserialize<List<DmnRule>>(RulesJson, JsonOpts) ?? new();
        return new DmnDecisionTable(Key, Name, inputs, outputs, rules, HitPolicy);
    }
}
