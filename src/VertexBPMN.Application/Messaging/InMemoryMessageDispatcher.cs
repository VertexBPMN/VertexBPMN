using System.Collections.Concurrent;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Entities.Modeling;
using VertexBPMN.Domain.Interfaces;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Application.Messaging;

/// <summary>
/// Einfacher In-Memory Dispatcher.
/// - Führt ServiceTasks direkt aus, falls lokaler Handler vorhanden.
/// - Hält Subscriptions für generische Engine-Messages sowie CaseFile-Updates.
/// - Puffer (thread-safe) für Tokens / Queue-Einträge (nützlich für Tests / Monitoring).
/// </summary>
public class InMemoryMessageDispatcher : IMessageDispatcher
{
    private readonly IServiceTaskRegistry _registry;

    // Subscriptions
    private readonly ConcurrentDictionary<string, ConcurrentBag<Func<Message, Task>>> _messageSubscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentBag<Func<CaseFileUpdateEvent, Task>>> _caseFileSubscriptions = new(StringComparer.OrdinalIgnoreCase);

    // Stored artifacts (optional inspection by tests)
    private readonly ConcurrentBag<ExecutionToken> _executionTokens = new();
    private readonly ConcurrentBag<CaseToken> _caseTokens = new();
    private readonly ConcurrentBag<(string TaskId, string TaskType, Dictionary<string, object> Variables)> _queuedTasks = new();
    private readonly ConcurrentBag<CaseFileUpdateEvent> _caseFileUpdates = new();

    public InMemoryMessageDispatcher(IServiceTaskRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Direkte (best effort) Ausführung eines ServiceTask-Handlers falls lokal registriert.
    /// Remote Simulation = No-Op.
    /// </summary>
    public Task DispatchServiceTaskAsync(string targetWorkerId, string implementation, Dictionary<string, string> attributes, Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        if (_registry.TryResolve(implementation, out var handler))
        {
            // Fire & forget; echte Engine würde ExecutionContext etc. mitgeben.
            _ = ExecuteHandlerSafeAsync(handler, attributes, variables, cancellationToken);
        }
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, object>> DispatchDmnTaskAsync(string targetWorker, string decisionRef, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
        => Task.FromResult(new Dictionary<string, object>(variables));

    public Task DispatchUserTaskAsync(string assignee, string taskId, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
        => Task.CompletedTask; // In-Memory: keine besondere Behandlung

    public Task PublishTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default)
    {
        _executionTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task PublishCaseTokenAsync(CaseToken token, CancellationToken cancellationToken = default)
    {
        _caseTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task QueueTaskAsync(string taskId, string taskType, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
    {
        _queuedTasks.Add((taskId, taskType, new Dictionary<string, object>(variables)));
        return Task.CompletedTask;
    }

    public Task SubscribeToMessageAsync(string messageName, Func<Message, Task> handler, CancellationToken cancellationToken = default)
    {
        var bag = _messageSubscriptions.GetOrAdd(messageName, _ => new ConcurrentBag<Func<Message, Task>>());
        bag.Add(handler);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Veröffentlicht eine generische Engine-Message an alle Subscriber.
    /// (Hilfsmethode – nicht Teil des Interfaces, kann intern / für Tests genutzt werden.)
    /// </summary>
    public Task PublishMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (_messageSubscriptions.TryGetValue(message.Name, out var handlers))
        {
            foreach (var h in handlers)
            {
                _ = InvokeSubscriberSafeAsync(() => h(message), cancellationToken);
            }
        }
        return Task.CompletedTask;
    }

    public Task PublishCaseFileUpdateAsync(CaseFileUpdateEvent updateEvent, CancellationToken cancellationToken = default)
    {
        _caseFileUpdates.Add(updateEvent);
        if (_caseFileSubscriptions.TryGetValue(updateEvent.CaseId, out var handlers))
        {
            foreach (var h in handlers)
            {
                _ = InvokeSubscriberSafeAsync(() => h(updateEvent), cancellationToken);
            }
        }
        return Task.CompletedTask;
    }

    public Task SubscribeToCaseFileUpdateAsync(string caseId, Func<CaseFileUpdateEvent, Task> handler, CancellationToken cancellationToken = default)
    {
        var bag = _caseFileSubscriptions.GetOrAdd(caseId, _ => new ConcurrentBag<Func<CaseFileUpdateEvent, Task>>());
        bag.Add(handler);
        return Task.CompletedTask;
    }

    public Task DispatchAiTaskAsync(string targetWorkerId, string aiProvider, string aiModel, Dictionary<string, string> attributes,
        Dictionary<string, object> variables, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    // ---------- Helpers / Safe Invocation ----------

    private static async Task ExecuteHandlerSafeAsync(IServiceTaskHandler handler, IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct)
    {
        try
        {
            await handler.ExecuteAsync(attributes, variables, ct).ConfigureAwait(false);
        }
        catch
        {
            // Schlucken für In-Memory (Testzwecke); produktiv: Logging + Incident
        }
    }

    private static async Task InvokeSubscriberSafeAsync(Func<Task> action, CancellationToken ct)
    {
        try
        {
            if (ct.IsCancellationRequested) return;
            await action().ConfigureAwait(false);
        }
        catch
        {
            // Intentionally ignored (Tests / isolierter InMemory Bus). In Produktion: ILogger nutzen.
        }
    }

    // ---------- Introspektion (optional für Tests) ----------
    public IReadOnlyCollection<ExecutionToken> ExecutionTokens => _executionTokens.ToArray();
    public IReadOnlyCollection<CaseToken> CaseTokens => _caseTokens.ToArray();
    public IReadOnlyCollection<(string TaskId, string TaskType, Dictionary<string, object> Variables)> QueuedTasks => _queuedTasks.ToArray();
    public IReadOnlyCollection<CaseFileUpdateEvent> CaseFileUpdates => _caseFileUpdates.ToArray();
}