using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Entities.Debugging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Debug;

/// <summary>
/// Builds process visualization data exclusively from persisted process state,
/// BPMN definition data, execution tokens, and history events.
/// </summary>
public sealed class PersistentProcessVisualizationService(
    BpmnDbContext db,
    IBpmnParser bpmnParser,
    ILogger<PersistentProcessVisualizationService> logger) : IProcessVisualizationService
{
    public async Task<ProcessVisualization> GetAsync(
        Guid processInstanceId,
        CancellationToken cancellationToken = default)
    {
        var instance = await db.ProcessInstances
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == processInstanceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Process instance '{processInstanceId}' was not found.");

        var definition = await db.ProcessDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == instance.ProcessDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Process definition '{instance.ProcessDefinitionId}' was not found.");

        if (string.IsNullOrWhiteSpace(definition.BpmnXml))
            throw new InvalidOperationException("The process definition has no BPMN XML for visualization.");

        var model = await bpmnParser.ParseAsync(definition.BpmnXml, cancellationToken);
        var tokens = await db.ExecutionTokens
            .AsNoTracking()
            .Where(value => value.ProcessInstanceId == processInstanceId)
            .OrderBy(value => value.CreatedAt)
            .ToListAsync(cancellationToken);
        var history = await db.HistoryEvents
            .AsNoTracking()
            .Where(value => value.ProcessInstanceId == processInstanceId)
            .OrderBy(value => value.Timestamp)
            .ToListAsync(cancellationToken);

        var activeTokens = tokens
            .Where(value => !string.Equals(value.State, "Completed", StringComparison.OrdinalIgnoreCase))
            .Select(value => new VisualToken
            {
                Id = value.Id,
                ActivityId = value.CurrentNodeId,
                Position = value.NodeType,
                Status = string.IsNullOrWhiteSpace(value.State) ? "Active" : value.State!
            })
            .ToList();

        var completedActivities = BuildCompletedActivities(history);
        var totalActivities = CountProcessNodes(model, definition.BpmnXml);
        var activeActivities = activeTokens
            .Select(value => value.ActivityId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var completedCount = completedActivities.Count;
        var end = instance.EndedAt ?? DateTime.UtcNow;
        var totalDuration = end >= instance.StartedAt
            ? end - instance.StartedAt
            : TimeSpan.Zero;

        var visualization = new ProcessVisualization
        {
            ProcessInstanceId = processInstanceId,
            ProcessDefinitionKey = definition.Key,
            BpmnXml = definition.BpmnXml,
            ActiveTokens = activeTokens,
            CompletedActivities = completedActivities,
            Metrics = new VisualizationMetrics
            {
                TotalActivities = totalActivities,
                CompletedActivities = completedCount,
                ActiveActivities = activeActivities,
                TotalDuration = totalDuration,
                AverageActivityDuration = completedCount == 0
                    ? TimeSpan.Zero
                    : TimeSpan.FromTicks(totalDuration.Ticks / completedCount)
            }
        };

        logger.LogInformation(
            "Loaded persisted process visualization for {ProcessInstanceId}: {ActiveTokenCount} active tokens, {CompletedActivityCount} completed activities",
            processInstanceId,
            activeTokens.Count,
            completedCount);

        return visualization;
    }

    private static List<VisualActivity> BuildCompletedActivities(IEnumerable<HistoryEvent> history)
    {
        var completed = new Dictionary<string, VisualActivity>(StringComparer.Ordinal);

        foreach (var entry in history)
        {
            if (string.Equals(entry.EventType, "VISUAL_DEBUG_STEP_OVER", StringComparison.OrdinalIgnoreCase))
            {
                AddCompleted(completed, ReadString(entry.Data, "startActivityId"), entry.Timestamp);

                if (ReadBoolean(entry.Data, "processCompleted"))
                    AddCompleted(completed, ReadString(entry.Data, "endActivityId"), entry.Timestamp);

                continue;
            }

            if (IsCompletionEvent(entry.EventType))
                AddCompleted(completed, entry.ElementId, entry.Timestamp);
        }

        return completed.Values
            .OrderBy(value => value.CompletedAt)
            .ToList();
    }

    private static bool IsCompletionEvent(string eventType) =>
        eventType.Contains("COMPLETED", StringComparison.OrdinalIgnoreCase)
        || eventType.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase)
        || eventType.Equals("PROCESS_ENDED", StringComparison.OrdinalIgnoreCase)
        || eventType.Equals("PROCESS_COMPLETED", StringComparison.OrdinalIgnoreCase);

    private static void AddCompleted(
        IDictionary<string, VisualActivity> completed,
        string? activityId,
        DateTime completedAt)
    {
        if (string.IsNullOrWhiteSpace(activityId) || completed.ContainsKey(activityId))
            return;

        completed[activityId] = new VisualActivity
        {
            ActivityId = activityId,
            Status = "Completed",
            CompletedAt = completedAt,
            ExecutionCount = 1
        };
    }

    private static string? ReadString(string? data, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(data))
            return null;

        try
        {
            using var document = JsonDocument.Parse(data);
            return document.RootElement.TryGetProperty(propertyName, out var property)
                ? property.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ReadBoolean(string? data, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(data))
            return false;

        try
        {
            using var document = JsonDocument.Parse(data);
            return document.RootElement.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int CountProcessNodes(
        VertexBPMN.Domain.Model.Bpmn.BpmnModel model,
        string bpmnXml)
    {
        var modelCount = model.Events.Count
            + model.Tasks.Count
            + model.Gateways.Count
            + model.Subprocesses.Count;

        var xmlCount = XDocument.Parse(bpmnXml)
            .Descendants()
            .Count(value => IsFlowNode(value.Name.LocalName));

        return xmlCount > 0 ? xmlCount : modelCount;
    }

    private static bool IsFlowNode(string localName) => localName switch
    {
        "startEvent" or "endEvent" or "intermediateCatchEvent" or "intermediateThrowEvent" or
        "boundaryEvent" or "task" or "userTask" or "serviceTask" or "scriptTask" or
        "manualTask" or "businessRuleTask" or "sendTask" or "receiveTask" or "callActivity" or
        "subProcess" or "exclusiveGateway" or "parallelGateway" or "inclusiveGateway" or
        "eventBasedGateway" or "complexGateway" => true,
        _ => false
    };
}
