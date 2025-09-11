using VertexBPMN.Core.Contracts;
using VertexBPMN.Core.Modeling;
using VertexBPMN.Domain;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Core.Messaging;

/// <summary>
/// Einfacher Dispatcher, der bei vorhandener lokale Handler-Registry direkt aufruft.
/// Nützlich für Unit-Tests und single-node Deployments.
/// </summary>
public class InMemoryMessageDispatcher : IMessageDispatcher
{
    private readonly IServiceTaskRegistry _registry;

    public InMemoryMessageDispatcher(IServiceTaskRegistry registry)
    {
        _registry = registry;
    }

    public Task<Dictionary<string, object>> DispatchDmnTaskAsync(string targetWorker, string decisionRef, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
        => Task.FromResult(new Dictionary<string, object>(variables));

    public Task DispatchUserTaskAsync(string assignee, string taskId, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DispatchServiceTaskAsync(string targetWorkerId, string implementation, Dictionary<string, string> attributes, Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        // Für Demo: Wenn ein lokaler Handler existiert, rufe ihn auf (synchron/async).
        if (_registry.TryResolve(implementation, out var handler))
        {
            _= handler.ExecuteAsync(attributes, variables, cancellationToken).ConfigureAwait(false);
        }

        // Sonst: Simuliere Remote-Dispatch: hier einfach Log / No-op
        return Task.CompletedTask;
    }

    public Task PublishTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishCaseTokenAsync(CaseToken token, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task QueueTaskAsync(string taskId, string taskType, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SubscribeToMessageAsync(string messageName, Func<Message, Task> handler, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishCaseFileUpdateAsync(CaseFileUpdateEvent updateEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SubscribeToCaseFileUpdateAsync(string caseId, Func<CaseFileUpdateEvent, Task> handler, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}