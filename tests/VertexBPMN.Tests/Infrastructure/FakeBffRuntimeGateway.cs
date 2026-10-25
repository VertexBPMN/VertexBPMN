using VertexBPMN.Portal.Bff;

namespace VertexBPMN.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="IBffRuntimeGateway"/> recording the server-derived values the
/// BFF resolves from the authenticated identity and the released revision. The portal
/// runtime slice tests never use the production HTTP gateway; they exercise the real
/// controllers, the narrow data-source resolver and the status projector with this fake
/// gateway as the only external-call seam.
/// </summary>
internal sealed class FakeBffRuntimeGateway : IBffRuntimeGateway
{
    public ReleasedApplication? Released { get; set; }
    public DataSourceResult DataSourceResult { get; set; } = new("invoiceList", "success", 3, DateTimeOffset.UtcNow);
    public BffTaskState State { get; set; } = new(Guid.Empty, "in_progress", null, null, null);

    public string? LastClaimedTenant { get; private set; }
    public Guid? LastClaimedTask { get; private set; }
    public string? LastClaimIdempotencyKey { get; private set; }
    public string? LastCompletedTenant { get; private set; }
    public Guid? LastCompletedTask { get; private set; }
    public string? LastExecutedBinding { get; private set; }
    public string? LastStartedTenant { get; private set; }
    public string? LastStartedProcessDefinitionId { get; private set; }
    public IDictionary<string, object?>? LastStartedVariables { get; private set; }
    public string? LastStartedIdempotencyKey { get; private set; }

    public void ResetCalls()
    {
        LastClaimedTenant = null;
        LastClaimedTask = null;
        LastClaimIdempotencyKey = null;
        LastCompletedTenant = null;
        LastCompletedTask = null;
        LastExecutedBinding = null;
        LastStartedTenant = null;
        LastStartedProcessDefinitionId = null;
        LastStartedVariables = null;
        LastStartedIdempotencyKey = null;
    }

    public Task<ReleasedApplication?> GetReleasedAsync(string tenantId, string applicationId, CancellationToken ct = default)
        => Task.FromResult(Released);

    public Task<DataSourceResult> ExecuteDataSourceReadAsync(string tenantId, string applicationId, string bindingName, CancellationToken ct = default)
    {
        LastExecutedBinding = bindingName;
        return Task.FromResult(DataSourceResult);
    }

    public Task StartProcessAsync(string tenantId, string processDefinitionId, IDictionary<string, object?>? variables, string idempotencyKey, CancellationToken ct = default)
    {
        LastStartedTenant = tenantId;
        LastStartedProcessDefinitionId = processDefinitionId;
        LastStartedVariables = variables;
        LastStartedIdempotencyKey = idempotencyKey;
        return Task.CompletedTask;
    }

    public Task ClaimAsync(string tenantId, string applicationId, Guid taskId, string idempotencyKey, CancellationToken ct = default)
    {
        LastClaimedTenant = tenantId;
        LastClaimedTask = taskId;
        LastClaimIdempotencyKey = idempotencyKey;
        return Task.CompletedTask;
    }

    public Task CompleteAsync(string tenantId, string applicationId, Guid taskId, string idempotencyKey, IReadOnlyDictionary<string, object?>? variables, CancellationToken ct = default)
    {
        LastCompletedTenant = tenantId;
        LastCompletedTask = taskId;
        return Task.CompletedTask;
    }

    public Task<BffTaskState> GetTaskStateAsync(string tenantId, string applicationId, Guid taskId, CancellationToken ct = default)
        => Task.FromResult(State with { TaskId = taskId });
}
