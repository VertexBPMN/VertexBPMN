using System.Security.Cryptography;
using System.Text;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application;

public sealed class WorkflowTriggerService(
    IWorkflowTriggerRepository triggerRepository,
    IRepositoryService repositoryService,
    IRuntimeService runtimeService) : IWorkflowTriggerService
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

    private static WorkflowTriggerInfo ToInfo(WorkflowTrigger trigger) => new(
        trigger.Id, trigger.Name, trigger.ProcessDefinitionKey, trigger.TenantId,
        trigger.Enabled, trigger.CreatedAt, trigger.LastModified,
        trigger.LastTriggeredAt, trigger.InvocationCount);

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
