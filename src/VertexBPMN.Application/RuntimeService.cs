using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application;

/// <summary>
/// Persistent implementation of IRuntimeService using IProcessInstanceRepository.
/// </summary>
public class RuntimeService : IRuntimeService
{
    private readonly IProcessInstanceRepository _repo;
    private readonly IProcessDefinitionRepository _defRepo;
    private readonly IProcessMiningEventSink _eventSink;
    private readonly IProcessExecutionRuntime _executionRuntime;
    public RuntimeService(
        IProcessInstanceRepository repo,
        IProcessDefinitionRepository defRepo,
        IProcessMiningEventSink eventSink,
        IProcessExecutionRuntime executionRuntime)
    {
        _repo = repo;
        _defRepo = defRepo;
        _eventSink = eventSink;
        _executionRuntime = executionRuntime;
    }

    // Vertex-kompatible Methoden
    public ValueTask<IDictionary<string, object>?> GetVariablesAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
        => GetVariablesCoreAsync(processInstanceId, cancellationToken);

    private async ValueTask<IDictionary<string, object>?> GetVariablesCoreAsync(Guid processInstanceId, CancellationToken cancellationToken)
        => (await _repo.GetByIdAsync(processInstanceId, cancellationToken))?.Variables;

    public async ValueTask<MessageCorrelationResult> CorrelateMessageAsync(string messageName, string? processInstanceId, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default, string? tenantId = null, string? idempotencyKey = null)
    {
        Guid? instanceId = Guid.TryParse(processInstanceId, out var parsed) ? parsed : null;
        if (instanceId.HasValue)
        {
            var instance = await _repo.GetByIdAsync(instanceId.Value, cancellationToken);
            if (instance is null || !string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal))
                return new MessageCorrelationResult("not_found", "", processInstanceId ?? "", "");
        }
        return await _executionRuntime.CorrelateMessageAsync(
            messageName, instanceId, variables, tenantId, idempotencyKey, cancellationToken);
    }

    public ValueTask BroadcastSignalAsync(string signalName, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default, string? tenantId = null, string? idempotencyKey = null)
        => _executionRuntime.BroadcastSignalAsync(signalName, variables, tenantId, idempotencyKey, cancellationToken);

    public async ValueTask<ProcessInstance> StartProcessByKeyAsync(string processDefinitionKey, IDictionary<string, object>? variables = null, string? businessKey = null, string? tenantId = null, CancellationToken cancellationToken = default, string? idempotencyKey = null)
    {
        // Lookup process definition by key
        var def = await _defRepo.GetLatestByKeyAsync(processDefinitionKey, tenantId, cancellationToken);
        if (def == null) throw new InvalidOperationException($"Process definition with key '{processDefinitionKey}' not found.");
        var instance = await _executionRuntime.StartAsync(
            def, variables, businessKey, tenantId, idempotencyKey, cancellationToken);
        // Emit process mining event
        await _eventSink.EmitAsync(new ProcessMiningEvent
        {
            EventType = "ProcessStarted",
            ProcessInstanceId = instance.Id.ToString(),
            TenantId = tenantId,
            Timestamp = DateTimeOffset.UtcNow,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                processDefinitionKey,
                variables = variables ?? new Dictionary<string, object>()
            })
        }, cancellationToken);
        return instance;
    }

    public ValueTask<ProcessInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(id, cancellationToken);

    public IAsyncEnumerable<ProcessInstance> ListAsync(Guid? processDefinitionId = null, string? tenantId = null, CancellationToken cancellationToken = default)
        => _repo.ListAsync(processDefinitionId, tenantId, cancellationToken);

    public async ValueTask SignalAsync(Guid processInstanceId, string signalName, object? payload = null, CancellationToken cancellationToken = default)
    {
        var inst = await _repo.GetByIdAsync(processInstanceId, cancellationToken);
        if (inst != null)
        {
            await _executionRuntime.BroadcastSignalAsync(
                signalName,
                payload as IDictionary<string, object>,
                inst.TenantId,
                cancellationToken: cancellationToken);
            await _eventSink.EmitAsync(new ProcessMiningEvent
            {
                EventType = "ProcessSignaled",
                ProcessInstanceId = inst.Id.ToString(),
                TenantId = inst.TenantId,
                Timestamp = DateTimeOffset.UtcNow,
                PayloadJson = payload is IDictionary<string, object> dict ? System.Text.Json.JsonSerializer.Serialize(dict) : null
            }, cancellationToken);
        }
    }

    public async ValueTask SuspendAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
    {
        var inst = await _repo.GetByIdAsync(processInstanceId, cancellationToken);
        if (inst != null)
        {
            if (inst.Status != ProcessInstanceStatus.Running)
                throw new InvalidOperationException($"Process instance is in {inst.Status} state.");
            inst.Status = ProcessInstanceStatus.Suspended;
            inst.State = "Suspended";
            inst.LastModified = DateTime.UtcNow;
            inst.Revision++;
            await _repo.UpdateAsync(inst, cancellationToken);
            await _eventSink.EmitAsync(new ProcessMiningEvent
            {
                EventType = "ProcessSuspended",
                ProcessInstanceId = inst.Id.ToString(),
                TenantId = inst.TenantId,
                Timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
    }

    public async ValueTask ResumeAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
    {
        var inst = await _repo.GetByIdAsync(processInstanceId, cancellationToken);
        if (inst != null)
        {
            if (inst.Status != ProcessInstanceStatus.Suspended)
                throw new InvalidOperationException($"Process instance is in {inst.Status} state.");
            inst.Status = ProcessInstanceStatus.Running;
            inst.State = "Waiting";
            inst.LastModified = DateTime.UtcNow;
            inst.Revision++;
            await _repo.UpdateAsync(inst, cancellationToken);
            await _eventSink.EmitAsync(new ProcessMiningEvent
            {
                EventType = "ProcessResumed",
                ProcessInstanceId = inst.Id.ToString(),
                TenantId = inst.TenantId,
                Timestamp = DateTimeOffset.UtcNow,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { processDefinitionKey = inst.ProcessId })
            }, cancellationToken);
        }
    }

    // Emits a process end event (for demo, call this when deleting from repository)
    public async ValueTask EndProcessAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
    {
        var inst = await _repo.GetByIdAsync(processInstanceId, cancellationToken);
        if (inst != null)
        {
            // Optionally: await _repo.DeleteAsync(processInstanceId, cancellationToken);
            await _eventSink.EmitAsync(new ProcessMiningEvent
            {
                EventType = "ProcessEnded",
                ProcessInstanceId = inst.Id.ToString(),
                TenantId = inst.TenantId,
                Timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
    }
}
