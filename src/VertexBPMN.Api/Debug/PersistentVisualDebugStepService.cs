using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Entities.Debugging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Debug;

/// <summary>
/// Moves a persisted execution token through exactly one BPMN sequence-flow step.
/// </summary>
public sealed class PersistentVisualDebugStepService(
    BpmnDbContext db,
    IBpmnParser bpmnParser,
    ILogger<PersistentVisualDebugStepService> logger) : IVisualDebugStepService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> InstanceLocks = new();

    public async Task<VisualDebugStepResult> StepAsync(
        Guid processInstanceId,
        CancellationToken cancellationToken = default)
    {
        var gate = InstanceLocks.GetOrAdd(processInstanceId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var instance = await db.ProcessInstances
                .SingleOrDefaultAsync(value => value.Id == processInstanceId, cancellationToken)
                ?? throw new KeyNotFoundException($"Process instance '{processInstanceId}' was not found.");

            if (instance.Status is ProcessInstanceStatus.Completed or ProcessInstanceStatus.Terminated)
                throw new InvalidOperationException($"Process instance '{processInstanceId}' is already completed.");

            var definition = await db.ProcessDefinitions
                .SingleOrDefaultAsync(value => value.Id == instance.ProcessDefinitionId, cancellationToken)
                ?? throw new KeyNotFoundException($"Process definition '{instance.ProcessDefinitionId}' was not found.");

            if (string.IsNullOrWhiteSpace(definition.BpmnXml))
                throw new InvalidOperationException("The process definition has no BPMN XML for visual stepping.");

            var model = await bpmnParser.ParseAsync(definition.BpmnXml, cancellationToken);
            var tokens = await db.ExecutionTokens
                .Where(value => value.ProcessInstanceId == processInstanceId)
                .OrderBy(value => value.CreatedAt)
                .ToListAsync(cancellationToken);

            var token = tokens.FirstOrDefault(value => !string.Equals(value.State, "Completed", StringComparison.OrdinalIgnoreCase));
            if (token is null)
            {
                var startEvent = model.Events.FirstOrDefault(value => value.Type == "startEvent")
                    ?? throw new InvalidOperationException("The process definition has no start event.");

                token = new ExecutionToken
                {
                    Id = Guid.NewGuid(),
                    ProcessInstanceId = processInstanceId,
                    CurrentNodeId = startEvent.Id,
                    NodeType = startEvent.Type,
                    CreatedAt = DateTime.UtcNow,
                    State = "Active"
                };
                db.ExecutionTokens.Add(token);
                tokens.Add(token);
            }

            var startActivityId = token.CurrentNodeId;
            var nextFlow = model.SequenceFlows
                .Where(value => value.SourceRef == startActivityId)
                .OrderByDescending(value => value.IsDefault)
                .ThenByDescending(value => value.Priority ?? int.MinValue)
                .ThenBy(value => value.Id, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    $"Activity '{startActivityId}' has no outgoing sequence flow for visual stepping.");

            var endActivityId = nextFlow.TargetRef;
            var endNodeType = GetNodeType(model, endActivityId, definition.BpmnXml);
            var timestamp = DateTime.UtcNow;
            var processCompleted = string.Equals(endNodeType, "endEvent", StringComparison.OrdinalIgnoreCase);

            token.CurrentNodeId = endActivityId;
            token.NodeType = endNodeType;
            token.State = processCompleted ? "Completed" : "Active";

            var activeTokenIds = tokens
                .Where(value => !string.Equals(value.State, "Completed", StringComparison.OrdinalIgnoreCase))
                .Select(value => value.Id == token.Id ? endActivityId : value.CurrentNodeId)
                .ToList();
            instance.ActiveTokens = activeTokenIds;
            instance.ActiveTasks = processCompleted || !IsTaskNode(endNodeType)
                ? new List<string>()
                : new List<string> { endActivityId };
            instance.State = processCompleted ? "Completed" : endActivityId;
            instance.Status = processCompleted ? ProcessInstanceStatus.Completed : ProcessInstanceStatus.Running;
            instance.EndedAt = processCompleted ? timestamp : null;
            instance.LastModified = timestamp;

            db.HistoryEvents.Add(new HistoryEvent
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = processInstanceId,
                EventType = "VISUAL_DEBUG_STEP_OVER",
                Timestamp = timestamp,
                Details = "Persisted visual-debug step-over",
                ElementId = endActivityId,
                TenantId = instance.TenantId,
                Data = JsonSerializer.Serialize(new
                {
                    startActivityId,
                    endActivityId,
                    endNodeType,
                    processCompleted
                })
            });

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Persisted visual-debug step-over for process {ProcessInstanceId}: {StartActivityId} -> {EndActivityId}",
                processInstanceId,
                startActivityId,
                endActivityId);

            return new VisualDebugStepResult
            {
                ProcessInstanceId = processInstanceId,
                TokenId = token.Id,
                StartActivityId = startActivityId,
                EndActivityId = endActivityId,
                EndNodeType = endNodeType,
                ProcessCompleted = processCompleted,
                Timestamp = timestamp,
                Instance = instance
            };
        }
        finally
        {
            gate.Release();
        }
    }

    private static string GetNodeType(
        VertexBPMN.Domain.Model.Bpmn.BpmnModel model,
        string activityId,
        string bpmnXml)
    {
        var modelType = model.Events.FirstOrDefault(value => value.Id == activityId)?.Type
            ?? model.Tasks.FirstOrDefault(value => value.Id == activityId)?.Type
            ?? model.Gateways.FirstOrDefault(value => value.Id == activityId)?.Type
            ?? (model.Subprocesses.Any(value => value.Id == activityId) ? "subProcess" : null);

        if (!string.IsNullOrWhiteSpace(modelType))
            return modelType;

        try
        {
            var element = XDocument.Parse(bpmnXml, LoadOptions.PreserveWhitespace)
                .Descendants()
                .FirstOrDefault(value => string.Equals(
                    value.Attribute("id")?.Value,
                    activityId,
                    StringComparison.Ordinal));
            return element?.Name.LocalName ?? "unknown";
        }
        catch (XmlException)
        {
            return "unknown";
        }
    }

    private static bool IsTaskNode(string nodeType) =>
        nodeType.EndsWith("Task", StringComparison.OrdinalIgnoreCase)
        || string.Equals(nodeType, "subProcess", StringComparison.OrdinalIgnoreCase);
}
