using VertexBPMN.Core.Cmmn;
using VertexBPMN.Core.Domain;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Core.Messaging;

public class NoOpMessageDispatcher : IMessageDispatcher
{
    public Task DispatchServiceTaskAsync(string targetWorker, string implementation, Dictionary<string, string> attributes, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<Dictionary<string, object>> DispatchDmnTaskAsync(string targetWorker, string decisionRef, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
        => Task.FromResult(new Dictionary<string, object>(variables));

    public Task DispatchUserTaskAsync(string assignee, string taskId, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

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