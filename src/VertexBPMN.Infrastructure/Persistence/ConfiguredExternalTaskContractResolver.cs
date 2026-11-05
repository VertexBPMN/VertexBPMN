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
		{
			throw new InvalidOperationException("external_task_input_mapping_invalid");
		}

		return ValueTask.CompletedTask;
    }

    private ExternalTaskCatalogEntry FindContract(string tenantId, ExternalTaskDefinition definition)
    {
        var matches = configuration.GetSection("ExternalTasks:Contracts").Get<ExternalTaskCatalogEntry[]>() ?? [];
        var candidates = matches.Where(entry => string.Equals(entry.TenantId, tenantId, StringComparison.Ordinal)
            && string.Equals(entry.Topic, definition.Topic, StringComparison.Ordinal)
            && string.Equals(entry.AgentProfileRef, definition.AgentProfileRef, StringComparison.Ordinal)).ToArray();
        if (string.IsNullOrWhiteSpace(tenantId) || candidates.Length != 1 || !candidates[0].Enabled)
		{
			throw new InvalidOperationException("external_task_contract_unavailable");
		}

		var contract = candidates[0];
        if (string.IsNullOrWhiteSpace(contract.Version) || contract.Version.Length > 128
            || contract.MaxAttempts is < 1 or > 10 || contract.MaxDeadlineSeconds is < 1 or > 86400
            || (definition.AgentProfileRef is not null && string.IsNullOrWhiteSpace(contract.AgentProfileVersion)))
		{
			throw new InvalidOperationException("external_task_invalid_contract");
		}

		if (definition.MaxAttempts > contract.MaxAttempts || definition.DeadlineSeconds > contract.MaxDeadlineSeconds)
		{
			throw new InvalidOperationException("external_task_contract_limits_exceeded");
		}

		if (contract.ObjectSchema is not null && !ValidateObjectSchema(contract.ObjectSchema))
		{
			throw new InvalidOperationException("external_task_invalid_contract");
		}

		ValidateFields(contract.Inputs);
        ValidateFields(contract.Outputs);
        if (contract.BusinessErrorCodes.Length > 32
            || contract.BusinessErrorCodes.Distinct(StringComparer.Ordinal).Count() != contract.BusinessErrorCodes.Length
            || contract.BusinessErrorCodes.Any(code => string.IsNullOrWhiteSpace(code) || code.Length > 128
                || !char.IsAsciiLetter(code[0]) || !char.IsLower(code[0])
                || code.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '.'))))
		{
			throw new InvalidOperationException("external_task_invalid_error_allowlist");
		}

		return contract;
    }

    public ValueTask<ResolvedExternalTaskContract> ResolveAsync(string tenantId, ExternalTaskDefinition definition,
        IReadOnlyDictionary<string, object> inputs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contract = FindContract(tenantId, definition);
        if (contract.ObjectSchema is not null)
        {
            return ResolveObjectContract(contract, inputs);
        }

        if (inputs.Keys.Any(key => !contract.Inputs.Any(field => field.Name == key)))
		{
			throw new InvalidOperationException("external_task_input_not_allowed");
		}

		foreach (var field in contract.Inputs)
        {
            if (!inputs.TryGetValue(field.Name, out var value))
            {
                if (field.Required)
				{
					throw new InvalidOperationException("external_task_input_missing");
				}

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
            if (!valid)
			{
				throw new InvalidOperationException("external_task_input_schema_invalid");
			}
		}
        if (System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(inputs)) > 128 * 1024)
		{
			throw new InvalidOperationException("external_task_input_too_large");
		}

		var snapshot = JsonSerializer.Serialize(new
        {
            dialect = "vertex.scalar-contract.v1", version = contract.Version,
            input = Schema(contract.Inputs), output = Schema(contract.Outputs),
            businessErrors = contract.BusinessErrorCodes.Order(StringComparer.Ordinal).ToArray()
        });
        return ValueTask.FromResult(new ResolvedExternalTaskContract(contract.Version, contract.AgentProfileVersion, snapshot));
    }

    private static bool ValidateObjectSchema(ExternalTaskObjectSchema schema)
    {
        if (schema.Version != "vertex.object-contract.v1"
            || schema.MaxDepth is < 1 or > 64
            || schema.MaxPropertyCount is < 1 or > 128
            || schema.MaxItemCount is < 1 or > 4096
            || schema.MaxBytes is < 1 or > (256 * 1024)
            || schema.Properties.Length is < 1 or > 32
            || schema.Properties.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != schema.Properties.Length)
        {
            return false;
        }

        return schema.Properties.All(field => !string.IsNullOrWhiteSpace(field.Name)
            && field.Name.Length <= 128
            && field.Name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')
            && field.Type is "string" or "boolean" or "integer" or "object" or "array"
            && field.AllowExternalTransfer
            && (field.Type != "string" || field.MaxLength is >= 1 and <= 65536));
    }

    private static ValueTask<ResolvedExternalTaskContract> ResolveObjectContract(
        ExternalTaskCatalogEntry contract, IReadOnlyDictionary<string, object> inputs)
    {
        // The whole structured payload is delivered as a single input value; it must never be
        // serialized to a string in place of a real JSON object (no JSON-in-string workaround).
        if (inputs.Count != 1)
        {
            throw new InvalidOperationException("external_task_input_schema_invalid");
        }

        var root = JsonSerializer.SerializeToElement(inputs.Single().Value);
        var schema = contract.ObjectSchema!;
        ValidateObjectTopLevel(root, schema);

        if (System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(root)) > schema.MaxBytes)
        {
            throw new InvalidOperationException("external_task_input_too_large");
        }

        var (maxDepth, totalCount) = MeasureObject(root);
        if (maxDepth > schema.MaxDepth || totalCount > schema.MaxItemCount)
        {
            throw new InvalidOperationException("external_task_input_schema_invalid");
        }

        var snapshot = JsonSerializer.Serialize(new
        {
            dialect = schema.Version, version = contract.Version,
            maxDepth = schema.MaxDepth, maxPropertyCount = schema.MaxPropertyCount,
            maxItemCount = schema.MaxItemCount, maxBytes = schema.MaxBytes,
            properties = schema.Properties.Select(field => new
            {
                field.Name, field.Type, field.Required, field.MaxLength
            }).ToArray(),
            businessErrors = contract.BusinessErrorCodes.Order(StringComparer.Ordinal).ToArray()
        });
        return ValueTask.FromResult(new ResolvedExternalTaskContract(contract.Version, contract.AgentProfileVersion, snapshot));
    }

    private static void ValidateObjectTopLevel(JsonElement root, ExternalTaskObjectSchema schema)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("external_task_input_schema_invalid");
        }

        var properties = schema.Properties.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var members = root.EnumerateObject().ToArray();
        if (members.Length > schema.MaxPropertyCount)
        {
            throw new InvalidOperationException("external_task_input_schema_invalid");
        }

        foreach (var member in members)
        {
            if (!properties.TryGetValue(member.Name, out var field))
            {
                throw new InvalidOperationException("external_task_input_not_allowed");
            }

            if (!MatchesType(member.Value, field))
            {
                throw new InvalidOperationException("external_task_input_schema_invalid");
            }
        }

        foreach (var field in schema.Properties)
        {
            if (field.Required && !members.Any(member => member.Name == field.Name))
            {
                throw new InvalidOperationException("external_task_input_missing");
            }
        }
    }

    private static (int MaxDepth, int TotalCount) MeasureObject(JsonElement root)
    {
        var totalCount = 0;
        var maxDepth = 0;
        void Walk(JsonElement element, int depth)
        {
            totalCount++;
            maxDepth = Math.Max(maxDepth, depth);
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var member in element.EnumerateObject())
                    {
                        Walk(member.Value, depth + 1);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Walk(item, depth + 1);
                    }
                    break;
            }
        }

        Walk(root, 1);
        return (maxDepth, totalCount);
    }

    private static bool MatchesType(JsonElement value, ExternalTaskObjectProperty field) => field.Type switch
    {
        "string" => value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= field.MaxLength,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        _ => false
    };

    private static void ValidateFields(ExternalTaskScalarField[] fields)
    {
        if (fields.Length > 32 || fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length
            || fields.Any(field => string.IsNullOrWhiteSpace(field.Name) || field.Name.Length > 128
                || field.Name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_'))
                || field.Type is not ("string" or "boolean" or "integer") || !field.AllowExternalTransfer
                || field.MaxLength is < 1 or > 65536))
		{
			throw new InvalidOperationException("external_task_invalid_contract_fields");
		}
	}

    private static object Schema(ExternalTaskScalarField[] fields) => new
    {
        type = "object", additionalProperties = false,
        required = fields.Where(field => field.Required).Select(field => field.Name).ToArray(),
        properties = fields.ToDictionary(field => field.Name, field => (object)(field.Type == "string"
            ? new Dictionary<string, object>(StringComparer.Ordinal) { ["type"] = field.Type, ["maxLength"] = field.MaxLength }
            : new Dictionary<string, object>(StringComparer.Ordinal) { ["type"] = field.Type }), StringComparer.Ordinal)
    };
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048", Justification = "Small DTO type kept adjacent to the contract resolver that serializes it; splitting it into a separate file adds no value.")]
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
    public ExternalTaskObjectSchema? ObjectSchema { get; set; }
    public string[] BusinessErrorCodes { get; set; } = [];
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048", Justification = "Small DTO type kept adjacent to the contract resolver that serializes it; splitting it into a separate file adds no value.")]
public sealed class ExternalTaskObjectSchema
{
    public string Version { get; set; } = "";
    public int MaxDepth { get; set; }
    public int MaxPropertyCount { get; set; }
    public int MaxItemCount { get; set; }
    public int MaxBytes { get; set; }
    public ExternalTaskObjectProperty[] Properties { get; set; } = [];
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048", Justification = "Small DTO type kept adjacent to the contract resolver that serializes it; splitting it into a separate file adds no value.")]
public sealed class ExternalTaskObjectProperty
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "string";
    public bool Required { get; set; } = true;
    public int MaxLength { get; set; } = 4096;
    public bool AllowExternalTransfer { get; set; }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048", Justification = "Small DTO type kept adjacent to the contract resolver that serializes it; splitting it into a separate file adds no value.")]
public sealed class ExternalTaskScalarField
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "string";
    public bool Required { get; set; } = true;
    public int MaxLength { get; set; } = 4096;
    public bool AllowExternalTransfer { get; set; }
}
