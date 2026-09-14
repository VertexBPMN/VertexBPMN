using System.Text.Json;
using Microsoft.Extensions.Configuration;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Infrastructure.Persistence;

/// <summary>
/// Local, operator-owned preview catalog. Supports bounded scalar input contracts;
/// it does not claim to implement arbitrary JSON Schema or infer whether text is a secret.
/// </summary>
public sealed class ConfiguredExternalTaskContractResolver(IConfiguration configuration) : IExternalTaskContractResolver
{
    public ValueTask ValidateDeploymentAsync(string tenantId, ExternalTaskDefinition definition,
        IReadOnlyCollection<string> inputNames, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contract = FindContract(tenantId, definition);
        if (inputNames.Distinct(StringComparer.Ordinal).Count() != inputNames.Count
            || inputNames.Any(name => !contract.Inputs.Any(field => field.Name == name))
            || contract.Inputs.Any(field => field.Required && !inputNames.Contains(field.Name, StringComparer.Ordinal)))
            throw new InvalidOperationException("external_task_input_mapping_invalid");
        return ValueTask.CompletedTask;
    }

    private ExternalTaskCatalogEntry FindContract(string tenantId, ExternalTaskDefinition definition)
    {
        var matches = configuration.GetSection("ExternalTasks:Contracts").Get<ExternalTaskCatalogEntry[]>() ?? [];
        var candidates = matches.Where(entry => string.Equals(entry.TenantId, tenantId, StringComparison.Ordinal)
            && string.Equals(entry.Topic, definition.Topic, StringComparison.Ordinal)
            && string.Equals(entry.AgentProfileRef, definition.AgentProfileRef, StringComparison.Ordinal)).ToArray();
        if (string.IsNullOrWhiteSpace(tenantId) || candidates.Length != 1 || !candidates[0].Enabled)
            throw new InvalidOperationException("external_task_contract_unavailable");
        var contract = candidates[0];
        if (string.IsNullOrWhiteSpace(contract.Version) || contract.Version.Length > 128
            || contract.MaxAttempts is < 1 or > 10 || contract.MaxDeadlineSeconds is < 1 or > 86400
            || (definition.AgentProfileRef is not null && string.IsNullOrWhiteSpace(contract.AgentProfileVersion)))
            throw new InvalidOperationException("external_task_invalid_contract");
        if (definition.MaxAttempts > contract.MaxAttempts || definition.DeadlineSeconds > contract.MaxDeadlineSeconds)
            throw new InvalidOperationException("external_task_contract_limits_exceeded");
        ValidateFields(contract.Inputs);
        ValidateFields(contract.Outputs);
        return contract;
    }

    public ValueTask<ResolvedExternalTaskContract> ResolveAsync(string tenantId, ExternalTaskDefinition definition,
        IReadOnlyDictionary<string, object> inputs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contract = FindContract(tenantId, definition);
        if (inputs.Keys.Any(key => !contract.Inputs.Any(field => field.Name == key)))
            throw new InvalidOperationException("external_task_input_not_allowed");
        foreach (var field in contract.Inputs)
        {
            if (!inputs.TryGetValue(field.Name, out var value))
            {
                if (field.Required) throw new InvalidOperationException("external_task_input_missing");
                continue;
            }
            // Object/array payloads (including credential-reference objects) are not accepted by this catalog.
            var json = JsonSerializer.SerializeToElement(value);
            var valid = field.Type switch
            {
                "string" => json.ValueKind == JsonValueKind.String && json.GetString()!.Length <= field.MaxLength,
                "boolean" => json.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "integer" => json.ValueKind == JsonValueKind.Number && json.TryGetInt64(out _),
                _ => false
            };
            if (!valid) throw new InvalidOperationException("external_task_input_schema_invalid");
        }
        if (System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(inputs)) > 128 * 1024)
            throw new InvalidOperationException("external_task_input_too_large");
        var snapshot = JsonSerializer.Serialize(new
        {
            dialect = "vertex.scalar-contract.v1", version = contract.Version,
            input = Schema(contract.Inputs), output = Schema(contract.Outputs)
        });
        return ValueTask.FromResult(new ResolvedExternalTaskContract(contract.Version, contract.AgentProfileVersion, snapshot));
    }

    private static void ValidateFields(ExternalTaskScalarField[] fields)
    {
        if (fields.Length > 32 || fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length
            || fields.Any(field => string.IsNullOrWhiteSpace(field.Name) || field.Name.Length > 128
                || field.Name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_'))
                || field.Type is not ("string" or "boolean" or "integer") || !field.AllowExternalTransfer
                || field.MaxLength is < 1 or > 65536))
            throw new InvalidOperationException("external_task_invalid_contract_fields");
    }

    private static object Schema(ExternalTaskScalarField[] fields) => new
    {
        type = "object", additionalProperties = false,
        required = fields.Where(field => field.Required).Select(field => field.Name).ToArray(),
        properties = fields.ToDictionary(field => field.Name, field => (object)(field.Type == "string"
            ? new Dictionary<string, object> { ["type"] = field.Type, ["maxLength"] = field.MaxLength }
            : new Dictionary<string, object> { ["type"] = field.Type }), StringComparer.Ordinal)
    };
}

public sealed class ExternalTaskCatalogEntry
{
    public string TenantId { get; set; } = "";
    public string Topic { get; set; } = "";
    public bool Enabled { get; set; }
    public string Version { get; set; } = "";
    public string? AgentProfileRef { get; set; }
    public string? AgentProfileVersion { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public int MaxDeadlineSeconds { get; set; } = 300;
    public ExternalTaskScalarField[] Inputs { get; set; } = [];
    public ExternalTaskScalarField[] Outputs { get; set; } = [];
}

public sealed class ExternalTaskScalarField
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "string";
    public bool Required { get; set; } = true;
    public int MaxLength { get; set; } = 4096;
    public bool AllowExternalTransfer { get; set; }
}
