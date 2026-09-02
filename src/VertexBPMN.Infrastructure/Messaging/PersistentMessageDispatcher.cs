using System.Text.Json;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Cmn;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Durable dispatcher that atomically accepts work into the runtime outbox.
/// Consumers can lease pending records without losing work during a restart.
/// Callback subscriptions are deliberately rejected because delegates cannot be
/// made durable; BPMN message subscriptions use EventSubscriptions instead.
/// </summary>
public sealed class PersistentMessageDispatcher(BpmnDbContext db) : IMessageDispatcher
{
    public Task DispatchServiceTaskAsync(string targetWorkerId, string implementation,
        Dictionary<string, string> attributes, Dictionary<string, object> variables,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync("ServiceTaskDispatch", Guid.Empty,
            new { targetWorkerId, implementation, attributes, variables }, cancellationToken);

    public Task PublishTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default) =>
        EnqueueAsync("ExecutionTokenPublished", token.ProcessInstanceId, token, cancellationToken);

    public Task PublishCaseTokenAsync(CaseToken token, CancellationToken cancellationToken = default) =>
        EnqueueAsync("CaseTokenPublished", Guid.Empty, token, cancellationToken);

    public Task QueueTaskAsync(string taskId, string taskType, Dictionary<string, object> variables,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync("TaskQueued", Guid.Empty, new { taskId, taskType, variables }, cancellationToken);

    public Task DispatchUserTaskAsync(string assignee, string taskId, Dictionary<string, object> variables,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync("UserTaskDispatch", Guid.Empty, new { assignee, taskId, variables }, cancellationToken);

    public Task<Dictionary<string, object>> DispatchDmnTaskAsync(string targetWorker, string decisionRef,
        Dictionary<string, object> variables, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Durable DMN request/reply dispatch is not part of the production BPMN runtime.");

    public Task SubscribeToMessageAsync(string messageName, Func<Message, Task> handler,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("In-process delegate subscriptions are not durable. Use BPMN EventSubscriptions.");

    public Task PublishCaseFileUpdateAsync(CaseFileUpdateEvent updateEvent,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync("CaseFileUpdated", Guid.Empty, updateEvent, cancellationToken);

    public Task SubscribeToCaseFileUpdateAsync(string caseId, Func<CaseFileUpdateEvent, Task> handler,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("In-process delegate subscriptions are not durable.");

    public Task DispatchAiTaskAsync(string targetWorkerId, string aiProvider, string aiModel,
        Dictionary<string, string> attributes, Dictionary<string, object> variables,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync("AiTaskDispatch", Guid.Empty,
            new { targetWorkerId, aiProvider, aiModel, attributes, variables }, cancellationToken);

    private async Task EnqueueAsync(string eventType, Guid processInstanceId, object payload,
        CancellationToken cancellationToken)
    {
        db.RuntimeOutbox.Add(new RuntimeOutboxMessage
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = processInstanceId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            State = "Pending",
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
