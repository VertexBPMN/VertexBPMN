using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Provides runtime operations for starting, signaling, and managing process instances.
/// </summary>

public interface IRuntimeService
{
    // Vertex-kompatible Variablen-API
    ValueTask<IDictionary<string, object>?> GetVariablesAsync(Guid processInstanceId, CancellationToken cancellationToken = default);

    // Vertex-kompatible Message-API
    ValueTask<MessageCorrelationResult> CorrelateMessageAsync(string messageName, string? processInstanceId, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default);

    // Vertex-kompatible Signal-API
    ValueTask BroadcastSignalAsync(string signalName, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default);

    // Vorhandene Methoden
    ValueTask<ProcessInstance> StartProcessByKeyAsync(string processDefinitionKey, IDictionary<string, object>? variables = null, string? businessKey = null, string? tenantId = null, CancellationToken cancellationToken = default);
    ValueTask<ProcessInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ProcessInstance> ListAsync(Guid? processDefinitionId = null, string? tenantId = null, CancellationToken cancellationToken = default);
    ValueTask SignalAsync(Guid processInstanceId, string signalName, object? payload = null, CancellationToken cancellationToken = default);
    ValueTask SuspendAsync(Guid processInstanceId, CancellationToken cancellationToken = default);
    ValueTask ResumeAsync(Guid processInstanceId, CancellationToken cancellationToken = default);
}

