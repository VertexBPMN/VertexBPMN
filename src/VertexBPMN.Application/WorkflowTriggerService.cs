using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application;

public sealed class WorkflowTriggerService(
    IWorkflowTriggerRepository triggerRepository,
    IRepositoryService repositoryService,
    IRuntimeService runtimeService,
    ICredentialService credentialService) : IWorkflowTriggerService
{
    public async Task<IReadOnlyList<WorkflowTriggerInfo>> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default)
        => (await triggerRepository.ListAsync(tenantId, cancellationToken)).Select(ToInfo).ToList();

    public async Task<WorkflowTriggerInfo?> GetAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var trigger = await triggerRepository.GetAsync(id, tenantId, cancellationToken);
        return trigger is null ? null : ToInfo(trigger);
    }

    public async Task<WorkflowTriggerCreated> CreateAsync(string name, string processDefinitionKey, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        name = Required(name, nameof(name), 256);
        processDefinitionKey = Required(processDefinitionKey, nameof(processDefinitionKey), 255);
        tenantId = Optional(tenantId, nameof(tenantId), 64);

        if (await repositoryService.GetLatestByKeyAsync(processDefinitionKey, tenantId, cancellationToken) is null)
            throw new WorkflowTriggerProcessNotFoundException(processDefinitionKey);

        var existing = await triggerRepository.ListAsync(tenantId, cancellationToken);
        if (existing.Any(trigger => string.Equals(trigger.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new WorkflowTriggerConflictException($"A trigger named '{name}' already exists in this tenant.");

        var secret = CreateSecret();
        var now = DateTime.UtcNow;
        var trigger = new WorkflowTrigger
        {
            Id = Guid.NewGuid(),
            Name = name,
            ProcessDefinitionKey = processDefinitionKey,
            TenantId = tenantId,
            SecretHash = HashSecret(secret),
            Enabled = true,
            CreatedAt = now,
            LastModified = now
        };
        await triggerRepository.AddAsync(trigger, cancellationToken);
        return new WorkflowTriggerCreated(ToInfo(trigger), secret, $"/api/triggers/{trigger.Id}/invoke");
    }

    public async Task<bool> UpdateAsync(Guid id, string? name, bool? enabled, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var trigger = await triggerRepository.GetAsync(id, tenantId, cancellationToken);
        if (trigger is null) return false;

        if (name is not null)
        {
            name = Required(name, nameof(name), 256);
            var existing = await triggerRepository.ListAsync(trigger.TenantId, cancellationToken);
            if (existing.Any(item => item.Id != id && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new WorkflowTriggerConflictException($"A trigger named '{name}' already exists in this tenant.");
            trigger.Name = name;
        }
        if (enabled.HasValue) trigger.Enabled = enabled.Value;
        trigger.LastModified = DateTime.UtcNow;
        await triggerRepository.SaveAsync(trigger, cancellationToken);
        return true;
    }

    public Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
        => triggerRepository.DeleteAsync(id, tenantId, cancellationToken);

    public async Task<WorkflowTriggerInvocationResult> InvokeAsync(Guid id, string secret, IDictionary<string, object?>? variables = null, string? businessKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return new(WorkflowTriggerInvocationStatus.InvalidSecret);

        var trigger = await triggerRepository.GetAsync(id, null, cancellationToken);
        if (trigger is null) return new(WorkflowTriggerInvocationStatus.NotFound);
        if (!CryptographicEquals(trigger.SecretHash, HashSecret(secret)))
            return new(WorkflowTriggerInvocationStatus.InvalidSecret);
        if (!trigger.Enabled) return new(WorkflowTriggerInvocationStatus.Disabled);

        if (await repositoryService.GetLatestByKeyAsync(trigger.ProcessDefinitionKey, trigger.TenantId, cancellationToken) is null)
            return new(WorkflowTriggerInvocationStatus.ProcessDefinitionNotFound);

        var runtimeVariables = variables?
            .Where(item => item.Value is not null)
            .ToDictionary(item => item.Key, item => item.Value!);
        var instance = await runtimeService.StartProcessByKeyAsync(
            trigger.ProcessDefinitionKey, runtimeVariables, businessKey, trigger.TenantId, cancellationToken);
        trigger.LastTriggeredAt = DateTime.UtcNow;
        trigger.InvocationCount++;
        trigger.LastModified = DateTime.UtcNow;
        await triggerRepository.SaveAsync(trigger, cancellationToken);
        return new(WorkflowTriggerInvocationStatus.Started, instance);
    }

    public async Task<IReadOnlyList<WorkflowTriggerCreated>> SynchronizeBpmnWebhooksAsync(string bpmnXml, string processDefinitionKey, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var document = XDocument.Parse(bpmnXml, LoadOptions.None);
        var vertex = XNamespace.Get("https://vertexbpmn.io/schema/bpmn/1.0");
        var bpmn = XNamespace.Get("http://www.omg.org/spec/BPMN/20100524/MODEL");
        var created = new List<WorkflowTriggerCreated>();
        foreach (var startEvent in document.Descendants(bpmn + "startEvent"))
        {
            var sourceElementId = (string?)startEvent.Attribute("id");
            var extensions = startEvent.Element(bpmn + "extensionElements");
            var webhook = extensions?.Elements().FirstOrDefault(element => element.Name == vertex + "webhook");
            if (webhook is null || string.IsNullOrWhiteSpace(sourceElementId)) continue;

            var path = NormalizePath((string?)webhook.Attribute("path"));
            var method = NormalizeMethod((string?)webhook.Attribute("method"));
            var trigger = extensions!.Elements().FirstOrDefault(element => element.Name == vertex + "trigger");
            var name = ((string?)trigger?.Attribute("name"))?.Trim();
            var authenticationMode = NormalizeAuthenticationMode((string?)webhook.Attribute("authMode"), (string?)webhook.Attribute("credentialRef") ?? (string?)webhook.Attribute("secretRef"));
            var credentialId = NormalizeOptional((string?)webhook.Attribute("credentialRef") ?? (string?)webhook.Attribute("secretRef"), 128);
            var found = await triggerRepository.GetBySourceElementAsync(processDefinitionKey, sourceElementId, tenantId, cancellationToken);
            WorkflowTrigger existing;
            string? oneTimeSecret = null;
            bool isNew;
            if (found is null)
            {
                isNew = true;
                oneTimeSecret = CreateSecret();
                existing = new WorkflowTrigger { Id = Guid.NewGuid(), SecretHash = HashSecret(oneTimeSecret), CreatedAt = DateTime.UtcNow };
            }
            else
            {
                isNew = false;
                existing = found;
            }
            var endpointOwner = await triggerRepository.GetByEndpointAsync(path, method, null, cancellationToken);
            if (endpointOwner is not null && endpointOwner.Id != existing.Id)
                throw new WorkflowTriggerConflictException($"Webhook endpoint '{method} {path}' is already registered.");

            existing.Name = string.IsNullOrWhiteSpace(name) ? $"Webhook {path}" : Required(name, nameof(name), 256);
            existing.ProcessDefinitionKey = processDefinitionKey;
            existing.TenantId = tenantId;
            existing.Path = path;
            existing.Method = method;
            existing.AuthenticationMode = authenticationMode;
            existing.CredentialId = credentialId;
            existing.CredentialSecretKey = NormalizeOptional((string?)webhook.Attribute("secretKey"), 128) ?? "secret";
            existing.PayloadSchemaJson = NormalizeOptional((string?)webhook.Attribute("payloadSchema"), 16_384);
            existing.CorrelationKey = NormalizeOptional((string?)webhook.Attribute("correlationKey"), 256);
            existing.SourceElementId = sourceElementId;
            existing.Enabled = true;
            existing.LastModified = DateTime.UtcNow;
            if (isNew)
            {
                await triggerRepository.AddAsync(existing, cancellationToken);
                // Reveal the one-time secret only for trigger-secret webhooks (HMAC
                // webhooks authenticate with the referenced credential secret instead).
                if (string.Equals(existing.AuthenticationMode, "trigger-secret", StringComparison.OrdinalIgnoreCase))
                    created.Add(new WorkflowTriggerCreated(ToInfo(existing), oneTimeSecret!, $"/api/webhooks{path}"));
            }
            else
            {
                await triggerRepository.SaveAsync(existing, cancellationToken);
            }
        }
        return created;
    }

    public async Task<WorkflowTriggerInvocationResult> InvokeWebhookAsync(string path, string method, string? triggerSecret, string? signature, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var trigger = await triggerRepository.GetByEndpointAsync(NormalizePath(path), NormalizeMethod(method), null, cancellationToken);
        if (trigger is null) return new(WorkflowTriggerInvocationStatus.NotFound);
        if (!trigger.Enabled) return new(WorkflowTriggerInvocationStatus.Disabled);

        if (string.Equals(trigger.AuthenticationMode, "hmac-sha256", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(trigger.TenantId) || string.IsNullOrWhiteSpace(trigger.CredentialId))
                return new(WorkflowTriggerInvocationStatus.InvalidSecret);
            var secret = await credentialService.ResolveSecretAsync(trigger.TenantId, trigger.CredentialId, trigger.CredentialSecretKey ?? "secret", cancellationToken);
            if (string.IsNullOrWhiteSpace(secret) || !IsValidHmac(secret, payload.Span, signature))
                return new(WorkflowTriggerInvocationStatus.InvalidSecret);
        }
        else if (string.IsNullOrWhiteSpace(triggerSecret) || !CryptographicEquals(trigger.SecretHash, HashSecret(triggerSecret)))
        {
            return new(WorkflowTriggerInvocationStatus.InvalidSecret);
        }

        var variables = ParsePayload(payload);
        if (!IsPayloadValid(trigger.PayloadSchemaJson, payload))
            return new(WorkflowTriggerInvocationStatus.InvalidPayload);
        var businessKey = ResolveCorrelationKey(trigger.CorrelationKey, variables);
        return await StartAsync(trigger, variables, businessKey, cancellationToken);
    }

    private async Task<WorkflowTriggerInvocationResult> StartAsync(WorkflowTrigger trigger, IDictionary<string, object?>? variables, string? businessKey, CancellationToken cancellationToken)
    {
        if (await repositoryService.GetLatestByKeyAsync(trigger.ProcessDefinitionKey, trigger.TenantId, cancellationToken) is null)
            return new(WorkflowTriggerInvocationStatus.ProcessDefinitionNotFound);
        var instance = await runtimeService.StartProcessByKeyAsync(trigger.ProcessDefinitionKey,
            variables?.Where(item => item.Value is not null).ToDictionary(item => item.Key, item => item.Value!), businessKey, trigger.TenantId, cancellationToken);
        trigger.LastTriggeredAt = DateTime.UtcNow;
        trigger.InvocationCount++;
        trigger.LastModified = DateTime.UtcNow;
        await triggerRepository.SaveAsync(trigger, cancellationToken);
        return new(WorkflowTriggerInvocationStatus.Started, instance);
    }

    private static WorkflowTriggerInfo ToInfo(WorkflowTrigger trigger) => new(
        trigger.Id, trigger.Name, trigger.ProcessDefinitionKey, trigger.TenantId,
        trigger.Enabled, trigger.CreatedAt, trigger.LastModified,
        trigger.LastTriggeredAt, trigger.InvocationCount, trigger.Path, trigger.Method,
        trigger.AuthenticationMode, trigger.CredentialId, trigger.CorrelationKey);

    private static string CreateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private static string HashSecret(string secret)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static bool CryptographicEquals(string expectedHex, string actualHex)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expectedHex), Convert.FromHexString(actualHex));

    private static bool IsValidHmac(string secret, ReadOnlySpan<byte> payload, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;
        var supplied = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase) ? signature[7..] : signature;
        byte[] provided;
        try { provided = Convert.FromHexString(supplied); }
        catch (FormatException) { return false; }
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private static IDictionary<string, object?> ParsePayload(ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty) return new Dictionary<string, object?>();
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new Dictionary<string, object?> { ["payload"] = document.RootElement.GetRawText() };
            return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => ToValue(property.Value));
        }
        catch (JsonException) { return new Dictionary<string, object?> { ["payload"] = Encoding.UTF8.GetString(payload.Span) }; }
    }

    // Deliberately supports the interoperable JSON Schema subset used by webhook contracts:
    // root type, required properties, and primitive property types.
    private static bool IsPayloadValid(string? schemaJson, ReadOnlyMemory<byte> payload)
    {
        if (string.IsNullOrWhiteSpace(schemaJson)) return true;
        try
        {
            using var schema = JsonDocument.Parse(schemaJson);
            using var document = JsonDocument.Parse(payload);
            var root = schema.RootElement;
            if (root.TryGetProperty("type", out var rootType) && !MatchesJsonType(document.RootElement, rootType.GetString())) return false;
            if (root.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
                foreach (var name in required.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()))
                    if (name is not null && !document.RootElement.TryGetProperty(name, out _)) return false;
            }
            if (root.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object && document.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var property in properties.EnumerateObject())
                    if (document.RootElement.TryGetProperty(property.Name, out var value) && property.Value.TryGetProperty("type", out var type) && !MatchesJsonType(value, type.GetString())) return false;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool MatchesJsonType(JsonElement value, string? type) => type switch
    {
        null => true, "object" => value.ValueKind == JsonValueKind.Object, "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String, "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False, "null" => value.ValueKind == JsonValueKind.Null,
        _ => false
    };

    private static object? ToValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(), JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => value.GetDouble(), JsonValueKind.True => true, JsonValueKind.False => false,
        JsonValueKind.Null => null, _ => value.GetRawText()
    };

    private static string? ResolveCorrelationKey(string? key, IDictionary<string, object?> variables) =>
        !string.IsNullOrWhiteSpace(key) && variables.TryGetValue(key, out var value) ? Convert.ToString(value) : null;

    private static string NormalizePath(string? value)
    {
        var path = Required(value, "path", 512);
        if (!path.StartsWith('/')) path = "/" + path;
        if (path.Contains("..", StringComparison.Ordinal) || path.Contains('?') || path.Contains('#')) throw new ArgumentException("path is invalid.", nameof(value));
        return path.TrimEnd('/').Length == 0 ? "/" : path.TrimEnd('/');
    }
    private static string NormalizeMethod(string? value) => Required(value ?? "POST", "method", 16).ToUpperInvariant();
    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maxLength) throw new ArgumentException($"value exceeds the maximum length of {maxLength}.", nameof(value));
        return normalized;
    }
    private static string NormalizeAuthenticationMode(string? value, string? credentialId)
    {
        var mode = string.IsNullOrWhiteSpace(value) ? (string.IsNullOrWhiteSpace(credentialId) ? "trigger-secret" : "hmac-sha256") : value.Trim().ToLowerInvariant();
        return mode is "trigger-secret" or "hmac-sha256" ? mode : throw new ArgumentException("authMode must be trigger-secret or hmac-sha256.", nameof(value));
    }

    private static string Required(string? value, string field, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException($"{field} is required.", field);
        if (normalized.Length > maxLength) throw new ArgumentException($"{field} exceeds the maximum length of {maxLength}.", field);
        return normalized;
    }

    private static string? Optional(string? value, string field, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maxLength) throw new ArgumentException($"{field} exceeds the maximum length of {maxLength}.", field);
        return normalized;
    }
}

public sealed class WorkflowTriggerConflictException(string message) : Exception(message);
public sealed class WorkflowTriggerProcessNotFoundException(string key)
    : Exception($"Process definition with key '{key}' was not found.");
