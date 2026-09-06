using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

public sealed class PollingTriggerService(
    IPollingTriggerRepository repository,
    PollingTriggerPoller poller) : IPollingTriggerService
{
    public async Task<IReadOnlyList<PollingTriggerInfo>> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var records = await repository.ListAsync(tenantId ?? "default", cancellationToken);
        return records.Select(ToInfo).ToList();
    }

    public async Task<PollingTriggerInfo?> GetAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var record = await repository.GetByIdAsync(id, cancellationToken);
        return record is null || !IsTenantMatch(record, tenantId) ? null : ToInfo(record);
    }

    public async Task<PollingTriggerCreated> CreateAsync(PollingTriggerWriteRequest request, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.ProcessDefinitionKey)) throw new ArgumentException("ProcessDefinitionKey is required.");
        if (string.IsNullOrWhiteSpace(request.ConnectorType)) throw new ArgumentException("ConnectorType is required.");

        var record = new PollingTriggerRecord
        {
            TenantId = tenantId ?? "default",
            Name = request.Name.Trim(),
            ProcessDefinitionKey = request.ProcessDefinitionKey.Trim(),
            ConnectorType = request.ConnectorType.Trim(),
            ConnectorAttributesJson = string.IsNullOrWhiteSpace(request.ConnectorAttributesJson) ? "{}" : request.ConnectorAttributesJson,
            CredentialId = request.CredentialId,
            IntervalSeconds = request.IntervalSeconds is > 0 ? request.IntervalSeconds.Value : 60,
            Enabled = request.Enabled ?? true,
            NextDueAt = DateTime.UtcNow // due immediately on creation so the scheduler/poll-now can run it right away
        };
        await repository.AddAsync(record, cancellationToken);
        return new PollingTriggerCreated(ToInfo(record));
    }

    public async Task<bool> UpdateAsync(Guid id, PollingTriggerWriteRequest? request = null, bool? enabled = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var record = await repository.GetByIdAsync(id, cancellationToken);
        if (record is null || !IsTenantMatch(record, tenantId)) return false;

        if (request is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.Name)) record.Name = request.Name.Trim();
            if (!string.IsNullOrWhiteSpace(request.ProcessDefinitionKey)) record.ProcessDefinitionKey = request.ProcessDefinitionKey.Trim();
            if (!string.IsNullOrWhiteSpace(request.ConnectorType)) record.ConnectorType = request.ConnectorType.Trim();
            if (request.ConnectorAttributesJson is not null) record.ConnectorAttributesJson = request.ConnectorAttributesJson;
            if (request.CredentialId is not null) record.CredentialId = request.CredentialId;
            if (request.IntervalSeconds is int interval and > 0) record.IntervalSeconds = interval;
            if (request.Enabled is bool enabledRequest) record.Enabled = enabledRequest;
        }
        if (enabled is bool enabledFlag) record.Enabled = enabledFlag;

        record.LastModified = DateTime.UtcNow;
        await repository.UpdateAsync(record, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var record = await repository.GetByIdAsync(id, cancellationToken);
        if (record is null || !IsTenantMatch(record, tenantId)) return false;
        return await repository.DeleteAsync(id, cancellationToken);
    }

    public async Task<PollingTriggerInfo?> PollNowAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var record = await repository.GetByIdAsync(id, cancellationToken);
        if (record is null || !IsTenantMatch(record, tenantId)) return null;

        var outcome = await poller.PollAsync(record, cancellationToken);
        PollingTriggerPoller.ApplyOutcome(record, outcome, DateTime.UtcNow);
        await repository.UpdateAsync(record, cancellationToken);
        return ToInfo(record);
    }

    private static bool IsTenantMatch(PollingTriggerRecord record, string? tenantId)
        => tenantId is null || string.IsNullOrWhiteSpace(record.TenantId) || record.TenantId == tenantId;

    private static PollingTriggerInfo ToInfo(PollingTriggerRecord record) => new(
        record.Id, record.TenantId, record.Name, record.ProcessDefinitionKey, record.ConnectorType,
        record.ConnectorAttributesJson, record.CredentialId, record.IntervalSeconds, record.CursorStateJson,
        record.Enabled, record.NextDueAt, record.LastPolledAt, record.ConsecutiveFailures,
        record.CreatedAt, record.LastModified);
}
