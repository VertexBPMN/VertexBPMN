using System.Collections.Concurrent;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.InMemory;

/// <summary>
/// In-memory implementation of IRuntimeService for development and testing.
/// </summary>
public class InMemoryRuntimeService : IRuntimeService
{
	// Vertex-kompatible Methoden
	public ValueTask<IDictionary<string, object>?> GetVariablesAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
		=> new((IDictionary<string, object>?)null);

	public ValueTask<MessageCorrelationResult> CorrelateMessageAsync(string messageName, string? processInstanceId, IDictionary<string, object>? variables = null, string? tenantId = null, string? idempotencyKey = null, CancellationToken cancellationToken = default)
		=> new(new MessageCorrelationResult("correlated", "", processInstanceId ?? string.Empty, ""));

	public ValueTask BroadcastSignalAsync(string signalName, IDictionary<string, object>? variables = null, string? tenantId = null, string? idempotencyKey = null, CancellationToken cancellationToken = default)
		=> ValueTask.CompletedTask;
	private readonly ConcurrentDictionary<Guid, ProcessInstance> _instances = new();

	private readonly IRepositoryService _repositoryService;
	private readonly IProcessMiningEventSink _eventSink;

	public InMemoryRuntimeService(IRepositoryService repositoryService, IProcessMiningEventSink eventSink)
	{
		_repositoryService = repositoryService;
		_eventSink = eventSink;
	}

	public async ValueTask<ProcessInstance> StartProcessByKeyAsync(string processDefinitionKey, IDictionary<string, object>? variables = null, string? businessKey = null, string? tenantId = null, string? idempotencyKey = null, CancellationToken cancellationToken = default)
	{
		// Lookup process definition by key
		var def = await _repositoryService.GetLatestByKeyAsync(processDefinitionKey, tenantId, cancellationToken);
		if (def == null)
		{
			throw new InvalidOperationException($"Process definition with key '{processDefinitionKey}' not found.");
		}

		var instance = new ProcessInstance
		{
			Id = Guid.NewGuid(),
			ProcessDefinitionId = def.Id,
			BusinessKey = businessKey,
			TenantId = tenantId,
			StartedAt = DateTime.UtcNow
		};
		_instances[instance.Id] = instance;
		// Emit process mining event
		await _eventSink.EmitAsync(new ProcessMiningEvent
		{
			EventType = "ProcessStarted",
			ProcessInstanceId = instance.Id.ToString(),
			TenantId = tenantId,
			Timestamp = DateTimeOffset.UtcNow,
			PayloadJson = variables != null ? System.Text.Json.JsonSerializer.Serialize(variables) : null
		}, cancellationToken);
		return instance;
	}

	public async ValueTask<ProcessInstance> StartProcessByIdAsync(Guid processDefinitionId, IDictionary<string, object>? variables = null, string? businessKey = null, string? tenantId = null, string? idempotencyKey = null, CancellationToken cancellationToken = default)
	{
		var def = await _repositoryService.GetByIdAsync(processDefinitionId, cancellationToken);
		if (def == null)
		{
			throw new InvalidOperationException($"Process definition with id '{processDefinitionId}' not found.");
		}

		var instance = new ProcessInstance
		{
			Id = Guid.NewGuid(),
			ProcessDefinitionId = def.Id,
			BusinessKey = businessKey,
			TenantId = tenantId,
			StartedAt = DateTime.UtcNow
		};
		_instances[instance.Id] = instance;
		await _eventSink.EmitAsync(new ProcessMiningEvent
		{
			EventType = "ProcessStarted",
			ProcessInstanceId = instance.Id.ToString(),
			TenantId = tenantId,
			Timestamp = DateTimeOffset.UtcNow,
			PayloadJson = variables != null ? System.Text.Json.JsonSerializer.Serialize(variables) : null
		}, cancellationToken);
		return instance;
	}

	public ValueTask<ProcessInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		=> ValueTask.FromResult(_instances.TryGetValue(id, out var inst) ? inst : null);

	public async IAsyncEnumerable<ProcessInstance> ListAsync(Guid? processDefinitionId = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var inst in _instances.Values)
		{
			if ((processDefinitionId == null || inst.ProcessDefinitionId == processDefinitionId) &&
				(tenantId == null || inst.TenantId == tenantId))
			{
				yield return inst;
			}
		}
		await Task.CompletedTask;
	}


	public async ValueTask SignalAsync(Guid processInstanceId, string signalName, object? payload = null, CancellationToken cancellationToken = default)
	{
		if (_instances.TryGetValue(processInstanceId, out var inst))
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
		if (_instances.TryGetValue(processInstanceId, out var inst))
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
		if (_instances.TryGetValue(processInstanceId, out var inst))
		{
			await _eventSink.EmitAsync(new ProcessMiningEvent
			{
				EventType = "ProcessResumed",
				ProcessInstanceId = inst.Id.ToString(),
				TenantId = inst.TenantId,
				Timestamp = DateTimeOffset.UtcNow
			}, cancellationToken);
		}
	}

	public async ValueTask DeleteAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
	{
		if (_instances.TryRemove(processInstanceId, out var inst))
		{
			await _eventSink.EmitAsync(new ProcessMiningEvent
			{
				EventType = "ProcessDeleted",
				ProcessInstanceId = inst.Id.ToString(),
				TenantId = inst.TenantId,
				Timestamp = DateTimeOffset.UtcNow
			}, cancellationToken);
		}
	}

	// Emits a process end event (for demo, call this when removing from _instances)
	public async ValueTask EndProcessAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
	{
		if (_instances.TryRemove(processInstanceId, out var inst))
		{
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
