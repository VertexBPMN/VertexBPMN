using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Forms;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Offline;

/// <summary>
/// Replays a client's offline task-completion queue to the server for exactly-once,
/// revision/schema-guarded completion (LC11 / LC-AC-19).
/// </summary>
public sealed class OfflineCompletionReplayService : IOfflineCompletionReplayService
{
    private readonly ITaskService _tasks;
    private readonly ITaskCompletionService _completion;

    public OfflineCompletionReplayService(ITaskService taskService, ITaskCompletionService completion)
    {
        _tasks = taskService;
        _completion = completion;
    }

    public async Task<IReadOnlyList<OfflineCompletionReplayResult>> ReplayAsync(
        IReadOnlyList<OfflineCompletionCommand> completions,
        string? userId, string? tenantId, bool isAdmin, IReadOnlyCollection<string>? roles,
        CancellationToken ct = default)
    {
        var results = new List<OfflineCompletionReplayResult>(completions.Count);
        foreach (var item in completions)
        {
            results.Add(await ReplayOneAsync(item, userId, tenantId, isAdmin, roles, ct));
        }

        return results;
    }

    private async Task<OfflineCompletionReplayResult> ReplayOneAsync(OfflineCompletionCommand item,
        string? userId, string? tenantId, bool isAdmin, IReadOnlyCollection<string>? roles, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(item.TaskId, ct);
        if (task is null)
        {
            return new(item.TaskId, OfflineCompletionStatus.NotFound);
        }

        // Server-side tenant authorization (mirrors the controller's CanAccessTenant).
        var tenantMismatch = isAdmin
            ? tenantId is not null && !string.Equals(tenantId, task.TenantId, StringComparison.Ordinal)
            : !string.Equals(tenantId, task.TenantId, StringComparison.Ordinal);
        if (tenantMismatch)
        {
            return new(item.TaskId, OfflineCompletionStatus.Forbidden);
        }

        // Client-supplied form data is never trusted: validate against the frozen task schema.
        var validation = TaskFormValidator.Validate(task, item.Variables);
        if (!validation.IsValid)
        {
            return new(item.TaskId, OfflineCompletionStatus.Conflict, "invalidFormData",
                task.Revision, task.FormSchemaHash);
        }

        // The idempotent completion contract re-checks identity, permission, revision and the
        // frozen schema on the CURRENT server state; it applies the effect at most once.
        try
        {
            await _completion.CompleteValidatedAsync(item.TaskId, item.Variables, item.IdempotencyKey,
                item.ExpectedRevision, item.FormSchemaHash, userId, task.TenantId, isAdmin, roles, ct);
            return new(item.TaskId, OfflineCompletionStatus.Completed);
        }
        catch (TaskCompletionConflictException)
        {
            return new(item.TaskId, OfflineCompletionStatus.Conflict, "staleTaskSnapshot",
                task.Revision, task.FormSchemaHash);
        }
        catch (UnauthorizedAccessException)
        {
            return new(item.TaskId, OfflineCompletionStatus.Forbidden);
        }
        catch (ArgumentException)
        {
            return new(item.TaskId, OfflineCompletionStatus.Conflict, "invalidFormData",
                task.Revision, task.FormSchemaHash);
        }
    }
}
