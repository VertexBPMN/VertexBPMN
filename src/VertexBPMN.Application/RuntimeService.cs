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
    public RuntimeService(IProcessInstanceRepository repo, IProcessDefinitionRepository defRepo, IProcessMiningEventSink eventSink)
    {
        _repo = repo;
        _defRepo = defRepo;
        _eventSink = eventSink;
    }

    // Vertex-kompatible Methoden
    public ValueTask<IDictionary<string, object>?> GetVariablesAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
        => new((IDictionary<string, object>?)null);

    public async ValueTask<MessageCorrelationResult> CorrelateMessageAsync(string messageName, string? processInstanceId, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageName)) throw new ArgumentException("messageName is required.", nameof(messageName));
        if (!Guid.TryParse(processInstanceId, out var instanceId))
            return new MessageCorrelationResult("not_found", "", processInstanceId ?? string.Empty, "");

        var instance = await _repo.GetByIdAsync(instanceId, cancellationToken);
        if (instance is null)
            return new MessageCorrelationResult("not_found", "", processInstanceId!, "");
        if (instance.Status is ProcessInstanceStatus.Completed or ProcessInstanceStatus.Terminated)
            return new MessageCorrelationResult("not_active", "", instance.Id.ToString(), instance.ProcessDefinitionId.ToString());

        foreach (var variable in variables ?? new Dictionary<string, object>()) instance.Variables[variable.Key] = variable.Value;
        instance.LastModified = DateTime.UtcNow;
        await _repo.UpdateAsync(instance, cancellationToken);
        await _eventSink.EmitAsync(new ProcessMiningEvent
        {
            EventType = "MessageCorrelated",
            ProcessInstanceId = instance.Id.ToString(),
            TenantId = instance.TenantId,
            Timestamp = DateTimeOffset.UtcNow,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { messageName, variables = variables ?? new Dictionary<string, object>() })
        }, cancellationToken);
        return new MessageCorrelationResult("correlated", instance.Id.ToString(), instance.Id.ToString(), instance.ProcessDefinitionId.ToString());
    }

    public ValueTask BroadcastSignalAsync(string signalName, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public async ValueTask<ProcessInstance> StartProcessByKeyAsync(string processDefinitionKey, IDictionary<string, object>? variables = null, string? businessKey = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        // Lookup process definition by key
        var def = await _defRepo.GetLatestByKeyAsync(processDefinitionKey, tenantId, cancellationToken);
        if (def == null) throw new InvalidOperationException($"Process definition with key '{processDefinitionKey}' not found.");
        var instance = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            ProcessDefinitionId = def.Id,
            ProcessId = processDefinitionKey,
            BusinessKey = businessKey,
            TenantId = tenantId,
            StartedAt = DateTime.UtcNow,
            Variables = variables is null ? [] : new Dictionary<string, object>(variables)
        };
        await _repo.AddAsync(instance, cancellationToken);
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
