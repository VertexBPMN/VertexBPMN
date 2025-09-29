using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Model.Cmn;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Abstraktion, um einen ServiceTask remote auszuführen (z. B. via RabbitMQ, Redis, HTTP, etc.).
/// Die Methode gibt optional ein Ergebnis-Map zurück (hier: void, weil ServiceTask direkt Variablen ändern kann).
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>
    /// Dispatch a remote service task execution request to a worker node.
    /// Returns when dispatch is accepted (not necessarily when remote finished).
    /// For testing you can implement sync behavior that actually executes the handler.
    /// </summary>
    Task DispatchServiceTaskAsync(string targetWorkerId, string implementation, Dictionary<string, string> attributes, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
    Task PublishTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default);
    Task PublishCaseTokenAsync(CaseToken token, CancellationToken cancellationToken = default);
    Task QueueTaskAsync(string taskId, string taskType, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
    Task DispatchUserTaskAsync(string assignee, string taskId, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> DispatchDmnTaskAsync(string targetWorker, string decisionRef, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
    Task SubscribeToMessageAsync(string messageName, Func<Message, Task> handler, CancellationToken cancellationToken = default);
    Task PublishCaseFileUpdateAsync(CaseFileUpdateEvent updateEvent, CancellationToken cancellationToken = default);
    Task SubscribeToCaseFileUpdateAsync(string caseId, Func<CaseFileUpdateEvent, Task> handler, CancellationToken cancellationToken = default);
    Task DispatchAiTaskAsync(string targetWorkerId, string aiProvider, string aiModel, Dictionary<string, string> attributes, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
}