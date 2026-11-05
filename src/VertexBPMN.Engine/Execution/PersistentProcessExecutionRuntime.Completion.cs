using System.Security.Cryptography;
using VertexBPMN.Domain.Interfaces;
using System.Text.Json;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Engine.Execution;

public sealed partial class PersistentProcessExecutionRuntime
{
    public ValueTask CompleteValidatedAsync(Guid taskId, IDictionary<string, object>? variables, string? idempotencyKey,
        long? expectedRevision, string? formSchemaHash, string? userId, string? tenantId, bool isAdmin,
        IReadOnlyCollection<string>? roles = null, CancellationToken ct = default)
        => CompleteTaskCoreAsync(taskId, variables, idempotencyKey,
            new TaskCompletionCommand(taskId, variables, idempotencyKey, expectedRevision, formSchemaHash, userId, tenantId, isAdmin, roles), ct);

    private static void AuthorizeCompletion(UserTask task, TaskCompletionCommand command)
    {
        if (!string.IsNullOrWhiteSpace(task.FormSchema) && string.IsNullOrWhiteSpace(command.UserId))
        {
            throw new UnauthorizedAccessException("Form-bound completion requires an authenticated caller identity.");
        }
        if (command.IsAdmin)
        {
            if (command.TenantId is not null && !string.Equals(command.TenantId, task.TenantId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("The task does not belong to the requested tenant.");
            }
            return;
        }
        if (string.IsNullOrWhiteSpace(command.UserId) || string.IsNullOrWhiteSpace(command.TenantId)
            || !string.Equals(command.TenantId, task.TenantId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The caller cannot complete this tenant's task.");
        }
        if (!string.IsNullOrWhiteSpace(task.Assignee))
        {
            if (!string.Equals(task.Assignee, command.UserId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("The caller is no longer the task assignee.");
            }
        }
        else if ((task.CandidateUsers.Count > 0 || !string.IsNullOrWhiteSpace(task.CandidateRole))
            && !task.CandidateUsers.Contains(command.UserId, StringComparer.Ordinal)
            && command.Roles?.Contains(task.CandidateRole, StringComparer.Ordinal) != true)
        {
            throw new UnauthorizedAccessException("The caller is not an authorized task candidate.");
        }
    }

    private static void FenceCompletion(UserTask task, TaskCompletionCommand command)
    {
        if (!string.IsNullOrWhiteSpace(task.FormSchema)
            && (command.ExpectedRevision is null || string.IsNullOrWhiteSpace(command.FormSchemaHash)
                || string.IsNullOrWhiteSpace(command.IdempotencyKey)))
        {
            throw new ArgumentException("Form-bound completion requires expectedRevision, formSchemaHash and an idempotency key.");
        }
        if (command.ExpectedRevision.HasValue && command.ExpectedRevision.Value != task.Revision)
        {
            throw new TaskCompletionConflictException("The task revision is stale.");
        }
        if (command.FormSchemaHash is not null && !string.Equals(command.FormSchemaHash, task.FormSchemaHash, StringComparison.Ordinal))
        {
            throw new TaskCompletionConflictException("The frozen task form schema has changed.");
        }
    }

    private static string CompletionFingerprint(Guid taskId, IDictionary<string, object>? variables, TaskCompletionCommand? command)
    {
        var payload = JsonSerializer.Serialize(new { variables = variables ?? new Dictionary<string, object>(StringComparer.Ordinal), command?.ExpectedRevision, command?.FormSchemaHash });
        using var document = JsonDocument.Parse(payload);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, document.RootElement);
        }
        return $"{taskId:N}:{Convert.ToHexString(SHA256.HashData(stream.ToArray()))}";
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray())
            {
                WriteCanonical(writer, item);
            }
            writer.WriteEndArray();
        }
        else
        {
            value.WriteTo(writer);
        }
    }

    private static void EnsureCompletionReplay(RuntimeInboxMessage inbox, Guid taskId, IDictionary<string, object>? variables, TaskCompletionCommand? command)
    {
        if (!string.Equals(inbox.Result, CompletionFingerprint(taskId, variables, command), StringComparison.Ordinal))
        {
            throw new TaskCompletionConflictException("The completion key has already been used with a different payload.");
        }
    }
}
