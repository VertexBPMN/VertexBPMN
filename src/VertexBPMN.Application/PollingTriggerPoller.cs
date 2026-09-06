using Microsoft.Extensions.Logging;
using System.Text.Json;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

public enum PollTriggerStatus { Idle, Started, Failed }

/// <summary>Result of a single polling cycle; carries the new cursor state and, on success, the started instance.</summary>
public sealed record PollTriggerOutcome(
    PollTriggerStatus Status,
    Guid? InstanceId,
    Dictionary<string, object> NewCursorState,
    string? ErrorCode = null)
{
    public static PollTriggerOutcome StartedInstance(Guid instanceId, Dictionary<string, object> cursor)
        => new(PollTriggerStatus.Started, instanceId, cursor);

    public static PollTriggerOutcome Idle(Dictionary<string, object> cursor)
        => new(PollTriggerStatus.Idle, null, cursor);

    public static PollTriggerOutcome Failed(string? code)
        => new(PollTriggerStatus.Failed, null, new Dictionary<string, object>(), code);
}

/// <summary>
/// Executes one polling cycle for a trigger: runs the configured connector, detects new data via the
/// cursor, and starts a process instance when new data is found. Shared by the background scheduler
/// and the synchronous <c>poll-now</c> endpoint.
/// </summary>
public sealed class PollingTriggerPoller(
    IConnectorRuntime connectorRuntime,
    IRuntimeService runtimeService,
    ILogger<PollingTriggerPoller> logger)
{
    private const double MaxBackoffSeconds = 3600;

    public async Task<PollTriggerOutcome> PollAsync(PollingTriggerRecord trigger, CancellationToken cancellationToken)
    {
        try
        {
            var attributes = DeserializeAttributes(trigger.ConnectorAttributesJson);
            var cursor = ParseCursor(trigger.CursorStateJson);
            var endpoint = attributes.TryGetValue("vertex:connector.endpoint", out var raw)
                && Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri : null;

            var context = new ConnectorExecutionContext(
                trigger.TenantId,
                trigger.ConnectorType,
                $"polling.{trigger.Id:N}",
                endpoint,
                attributes,
                new Dictionary<string, object>(cursor),
                new ConnectorRetryPolicy(2, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(500)),
                trigger.CredentialId);

            var result = await connectorRuntime.ExecuteAsync(context, cancellationToken);
            if (!result.Success)
            {
                logger.LogWarning("Polling trigger {TriggerId} ({Type}) failed: {Code}", trigger.Id, trigger.ConnectorType, result.ErrorCode);
                return PollTriggerOutcome.Failed(result.ErrorCode);
            }

            var outputs = new Dictionary<string, object>(result.Outputs ?? new Dictionary<string, object>());
            var cursorField = attributes.TryGetValue("vertex:polling.cursorField", out var field) ? field : null;
            if (!IsNewData(cursor, outputs, cursorField, out var newCursor))
                return PollTriggerOutcome.Idle(newCursor);

            var instance = await runtimeService.StartProcessByKeyAsync(
                trigger.ProcessDefinitionKey, outputs, null, trigger.TenantId, cancellationToken);
            logger.LogInformation("Polling trigger {TriggerId} started instance {InstanceId}", trigger.Id, instance.Id);
            return PollTriggerOutcome.StartedInstance(instance.Id, newCursor);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Polling trigger {TriggerId} failed unexpectedly", trigger.Id);
            return PollTriggerOutcome.Failed("poll_error");
        }
    }

    /// <summary>Applies an outcome to a trigger (cursor, next due, lock release, failure counter/backoff).</summary>
    public static void ApplyOutcome(PollingTriggerRecord trigger, PollTriggerOutcome outcome, DateTime asOf)
    {
        trigger.LastPolledAt = asOf;
        trigger.LockOwner = null;
        trigger.LockedUntil = null;

        if (outcome.Status == PollTriggerStatus.Failed)
        {
            trigger.ConsecutiveFailures++;
            trigger.NextDueAt = asOf.Add(Backoff(trigger.IntervalSeconds, trigger.ConsecutiveFailures));
        }
        else
        {
            trigger.ConsecutiveFailures = 0;
            trigger.CursorStateJson = JsonSerializer.Serialize(outcome.NewCursorState);
            trigger.NextDueAt = asOf.AddSeconds(trigger.IntervalSeconds);
        }
        trigger.LastModified = asOf;
    }

    private static TimeSpan Backoff(int intervalSeconds, int failures)
        => TimeSpan.FromSeconds(Math.Min(intervalSeconds * Math.Pow(2, failures - 1), MaxBackoffSeconds));

    private static bool IsNewData(
        Dictionary<string, object> cursor, Dictionary<string, object> outputs, string? cursorField, out Dictionary<string, object> newCursor)
    {
        if (!string.IsNullOrWhiteSpace(cursorField) && outputs.TryGetValue(cursorField, out var value) && value is not null)
        {
            newCursor = new Dictionary<string, object>(cursor) { [cursorField] = value };
            return !cursor.TryGetValue(cursorField, out var previous) || !Equals(previous, value);
        }

        newCursor = new Dictionary<string, object>(outputs);
        return JsonSerializer.Serialize(cursor) != JsonSerializer.Serialize(outputs);
    }

    private static Dictionary<string, string> DeserializeAttributes(string json)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

    private static Dictionary<string, object> ParseCursor(string json)
        => JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
}
