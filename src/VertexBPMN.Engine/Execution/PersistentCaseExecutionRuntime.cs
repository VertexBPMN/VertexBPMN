using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Cmn;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Engine.Execution;

public sealed class PersistentCaseExecutionRuntime(
    BpmnDbContext db,
    ICmmnParser parser,
    IServiceTaskRegistry serviceTasks) : ICaseExecutionRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CaseDefinitionRecord> DeployAsync(
        string key,
        string name,
        string cmmnXml,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(cmmnXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        await parser.ParseAsync(cmmnXml, cancellationToken);
        if (await db.CaseDefinitions.AnyAsync(x => x.TenantId == tenantId && x.Key == key, cancellationToken))
            throw new InvalidOperationException($"CMMN definition '{key}' already exists for tenant '{tenantId}'.");

        var now = DateTime.UtcNow;
        var definition = new CaseDefinitionRecord
        {
            Id = Guid.NewGuid().ToString(),
            Key = key.Trim(),
            Name = name.Trim(),
            CmmnXml = cmmnXml,
            TenantId = tenantId.Trim(),
            CreatedAt = now,
            LastModified = now
        };
        db.CaseDefinitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken);
        return definition;
    }

    public Task<CaseDefinitionRecord?> GetDefinitionAsync(
        string key,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        db.CaseDefinitions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Key == key && x.TenantId == tenantId,
            cancellationToken);

    public async Task<CaseExecutionResult> StartAsync(
        string key,
        string tenantId,
        IReadOnlyDictionary<string, object>? caseFile = null,
        CancellationToken cancellationToken = default)
    {
        var definition = await GetDefinitionAsync(key, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"CMMN definition '{key}' was not found for tenant '{tenantId}'.");
        var model = await parser.ParseAsync(definition.CmmnXml, cancellationToken);
        var values = model.CaseFileItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(item => item.Id, item => item.Value, StringComparer.Ordinal);
        if (caseFile is not null)
            foreach (var item in caseFile)
                values[item.Key] = item.Value;

        var states = model.PlanItems.ToDictionary(
            item => item.Id,
            item => item.IsDiscretionary ? "Discretionary" : "Available",
            StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var instance = new CaseInstanceRecord
        {
            Id = Guid.NewGuid(),
            CaseDefinitionId = definition.Id,
            CaseDefinitionKey = definition.Key,
            TenantId = tenantId,
            State = "Active",
            CaseFileJson = JsonSerializer.Serialize(values, JsonOptions),
            PlanItemStatesJson = JsonSerializer.Serialize(states, JsonOptions),
            CreatedAt = now,
            LastModified = now,
            Revision = 1
        };
        db.CaseInstances.Add(instance);
        var trace = new List<string> { $"CASE_STARTED:{instance.Id}" };
        await AdvanceAsync(instance, model, values, states, trace, cancellationToken);
        await PersistAsync(instance, values, states, trace, cancellationToken);
        return new CaseExecutionResult(instance, trace);
    }

    public async Task<CaseExecutionResult> CompletePlanItemAsync(
        Guid caseInstanceId,
        string planItemId,
        IReadOnlyDictionary<string, object>? caseFileUpdates = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var (instance, model, values, states) = await LoadAsync(caseInstanceId, tenantId, cancellationToken);
        if (!states.TryGetValue(planItemId, out var state) || state != "Active")
            throw new InvalidOperationException($"Plan item '{planItemId}' is not active.");
        Merge(values, caseFileUpdates);
        states[planItemId] = "Completed";
        var trace = new List<string> { $"PLAN_ITEM_COMPLETED:{planItemId}" };
        await AdvanceAsync(instance, model, values, states, trace, cancellationToken);
        await PersistAsync(instance, values, states, trace, cancellationToken);
        return new CaseExecutionResult(instance, trace);
    }

    public async Task<CaseExecutionResult> TriggerUserEventAsync(
        Guid caseInstanceId,
        string eventId,
        IReadOnlyDictionary<string, object>? eventData = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var (instance, model, values, states) = await LoadAsync(caseInstanceId, tenantId, cancellationToken);
        var listener = model.PlanItems.FirstOrDefault(item =>
            states.GetValueOrDefault(item.Id) == "Active"
            && NormalizeType(item.Type) is "eventlistener" or "usereventlistener"
            && (item.Id == eventId || item.DefinitionRef == eventId
                || item.Attributes?.GetValueOrDefault("name") == eventId));
        if (listener is null)
            throw new InvalidOperationException($"No active user event listener matches '{eventId}'.");
        Merge(values, eventData);
        states[listener.Id] = "Completed";
        var trace = new List<string> { $"USER_EVENT_TRIGGERED:{listener.Id}" };
        await AdvanceAsync(instance, model, values, states, trace, cancellationToken);
        await PersistAsync(instance, values, states, trace, cancellationToken);
        return new CaseExecutionResult(instance, trace);
    }

    public async Task<CaseExecutionResult> UpdateCaseFileItemAsync(
        Guid caseInstanceId,
        string itemId,
        object? value,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var (instance, model, values, states) = await LoadAsync(caseInstanceId, tenantId, cancellationToken);
        values[itemId] = value!;
        var trace = new List<string> { $"CASE_FILE_UPDATED:{itemId}" };
        await AdvanceAsync(instance, model, values, states, trace, cancellationToken);
        await PersistAsync(instance, values, states, trace, cancellationToken);
        return new CaseExecutionResult(instance, trace);
    }

    public async Task<CaseExecutionResult> ActivateDiscretionaryItemAsync(
        Guid caseInstanceId,
        string planItemId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var (instance, model, values, states) = await LoadAsync(caseInstanceId, tenantId, cancellationToken);
        var planItem = model.PlanItems.SingleOrDefault(item => item.Id == planItemId && item.IsDiscretionary)
            ?? throw new KeyNotFoundException($"Discretionary plan item '{planItemId}' was not found.");
        if (states.GetValueOrDefault(planItem.Id) != "Discretionary")
            throw new InvalidOperationException($"Discretionary plan item '{planItemId}' has already been activated.");
        states[planItem.Id] = "Available";
        var trace = new List<string> { $"DISCRETIONARY_ITEM_ACTIVATED:{planItem.Id}" };
        await AdvanceAsync(instance, model, values, states, trace, cancellationToken);
        await PersistAsync(instance, values, states, trace, cancellationToken);
        return new CaseExecutionResult(instance, trace);
    }

    public Task<CaseInstanceRecord?> GetInstanceAsync(
        Guid caseInstanceId,
        string? tenantId = null,
        CancellationToken cancellationToken = default) =>
        db.CaseInstances.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == caseInstanceId && (tenantId == null || x.TenantId == tenantId),
            cancellationToken);

    public async Task<CaseInstanceRecord?> ResolveInstanceAsync(
        string instanceIdOrDefinitionKey,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(instanceIdOrDefinitionKey, out var id))
            return await GetInstanceAsync(id, tenantId, cancellationToken);
        return await db.CaseInstances.AsNoTracking()
            .Where(instance => instance.TenantId == tenantId
                               && instance.CaseDefinitionKey == instanceIdOrDefinitionKey
                               && instance.State == "Active")
            .OrderByDescending(instance => instance.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CaseHistoryEntry>> GetHistoryAsync(
        Guid caseInstanceId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId is not null && !await db.CaseInstances.AnyAsync(
                instance => instance.Id == caseInstanceId && instance.TenantId == tenantId,
                cancellationToken))
            return [];
        var history = await db.CmmnHistory.AsNoTracking()
            .Where(entry => entry.CaseId == caseInstanceId.ToString())
            .OrderBy(entry => entry.Timestamp)
            .ToListAsync(cancellationToken);
        return history.Select(entry => new CaseHistoryEntry(
            caseInstanceId,
            JsonSerializer.Deserialize<Dictionary<string, object?>>(entry.CaseFileJson, JsonOptions) ?? [],
            JsonSerializer.Deserialize<List<string>>(entry.CompletedPlanItemsJson, JsonOptions) ?? [],
            entry.Timestamp)).ToList();
    }

    private async Task AdvanceAsync(
        CaseInstanceRecord instance,
        CaseModel model,
        Dictionary<string, object> values,
        Dictionary<string, string> states,
        List<string> trace,
        CancellationToken cancellationToken)
    {
        var completedSomething = true;
        var passes = 0;
        while (completedSomething)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++passes > 1_000) throw new InvalidOperationException("CMMN lifecycle exceeded 1,000 activation passes.");
            completedSomething = false;
            foreach (var active in model.PlanItems.Where(item =>
                         states.GetValueOrDefault(item.Id) == "Active"
                         && item.ExitSentryRefs is { Count: > 0 }
                         && SentriesSatisfied(item.ExitSentryRefs, model, values, states)))
            {
                states[active.Id] = "Terminated";
                trace.Add($"PLAN_ITEM_TERMINATED:{active.Id}:exit-sentry");
                completedSomething = true;
            }

            foreach (var item in model.PlanItems)
            {
                if (states.GetValueOrDefault(item.Id) != "Available" || !SentriesSatisfied(item.EntrySentryRefs, model, values, states))
                    continue;
                if (item.ParentPlanItemId is { Length: > 0 } parentId
                    && states.GetValueOrDefault(parentId) != "Active")
                    continue;

                var type = NormalizeType(item.Type);
                switch (type)
                {
                    case "humantask":
                    case "manualtask":
                    case "eventlistener":
                    case "usereventlistener":
                        states[item.Id] = "Active";
                        trace.Add($"PLAN_ITEM_ACTIVE:{item.Id}:{type}");
                        break;
                    case "servicetask":
                        var implementation = item.Attributes?.GetValueOrDefault("type")
                                             ?? item.Attributes?.GetValueOrDefault("implementation")
                                             ?? item.DefinitionRef;
                        if (string.IsNullOrWhiteSpace(implementation)
                            || !serviceTasks.TryResolve(implementation, out var handler)
                            || handler is null)
                            throw new InvalidOperationException($"No service-task handler is registered for CMMN plan item '{item.Id}'.");
                        await handler.ExecuteAsync(item.Attributes ?? [], values, cancellationToken);
                        states[item.Id] = "Completed";
                        trace.Add($"PLAN_ITEM_COMPLETED:{item.Id}:servicetask");
                        completedSomething = true;
                        break;
                    case "milestone":
                    case "task":
                        states[item.Id] = "Completed";
                        trace.Add($"PLAN_ITEM_COMPLETED:{item.Id}:{type}");
                        completedSomething = true;
                        break;
                    case "stage":
                        states[item.Id] = "Active";
                        trace.Add($"PLAN_ITEM_ACTIVE:{item.Id}:stage");
                        completedSomething = true;
                        break;
                    default:
                        throw new NotSupportedException($"CMMN plan-item type '{item.Type}' is not executable.");
                }
            }

            foreach (var stage in model.PlanItems.Where(item =>
                         NormalizeType(item.Type) == "stage"
                         && states.GetValueOrDefault(item.Id) == "Active"))
            {
                var children = model.PlanItems.Where(item => item.ParentPlanItemId == stage.Id && !item.IsDiscretionary).ToArray();
                if (children.All(child => states.GetValueOrDefault(child.Id) is "Completed" or "Terminated"))
                {
                    states[stage.Id] = "Completed";
                    trace.Add($"PLAN_ITEM_COMPLETED:{stage.Id}:stage");
                    completedSomething = true;
                }
            }
        }

        var required = model.PlanItems.Where(item => !item.IsDiscretionary).Select(item => item.Id).ToArray();
        if (required.All(id => states.GetValueOrDefault(id) is "Completed" or "Terminated"))
        {
            instance.State = "Completed";
            instance.CompletedAt = DateTime.UtcNow;
            trace.Add("CASE_COMPLETED");
        }
        else
        {
            instance.State = "Active";
        }
    }

    private static bool SentriesSatisfied(
        IReadOnlyCollection<string>? sentryRefs,
        CaseModel model,
        IReadOnlyDictionary<string, object> values,
        IReadOnlyDictionary<string, string> states)
    {
        if (sentryRefs is null || sentryRefs.Count == 0) return true;
        return sentryRefs.All(reference =>
        {
            var sentry = model.Sentries.FirstOrDefault(item => item.Id == reference)
                         ?? throw new InvalidOperationException($"Sentry '{reference}' was not found.");
            var onPartSatisfied = string.IsNullOrWhiteSpace(sentry.OnPartRef)
                                  || states.GetValueOrDefault(sentry.OnPartRef) is "Completed" or "Terminated"
                                  || (!states.ContainsKey(sentry.OnPartRef) && values.ContainsKey(sentry.OnPartRef));
            return onPartSatisfied && sentry.Conditions.All(condition => EvaluateCondition(condition.Expression, values));
        });
    }

    private static bool EvaluateCondition(string expression, IReadOnlyDictionary<string, object> values)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;
        var normalized = expression.Trim();
        if ((normalized.StartsWith("${", StringComparison.Ordinal) || normalized.StartsWith("#{", StringComparison.Ordinal))
            && normalized.EndsWith('}')) normalized = normalized[2..^1];
        normalized = Regex.Replace(normalized, @"(?<![<>=!])=(?!=)", "==", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\band\b", "&&", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\bor\b", "||", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var engine = new Jint.Engine(options => options.TimeoutInterval(TimeSpan.FromMilliseconds(100)).LimitRecursion(64).MaxStatements(1_000));
        foreach (var item in values) engine.SetValue(item.Key, Normalize(item.Value));
        return engine.Evaluate(normalized).AsBoolean();
    }

    private async Task<(CaseInstanceRecord Instance, CaseModel Model, Dictionary<string, object> Values, Dictionary<string, string> States)> LoadAsync(
        Guid id,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        var instance = await db.CaseInstances.SingleOrDefaultAsync(
            x => x.Id == id && (tenantId == null || x.TenantId == tenantId), cancellationToken)
            ?? throw new KeyNotFoundException($"Case instance '{id}' was not found.");
        if (instance.State != "Active") throw new InvalidOperationException($"Case instance '{id}' is {instance.State}.");
        var definition = await db.CaseDefinitions.AsNoTracking().SingleAsync(x => x.Id == instance.CaseDefinitionId, cancellationToken);
        var model = await parser.ParseAsync(definition.CmmnXml, cancellationToken);
        return (instance, model,
            JsonSerializer.Deserialize<Dictionary<string, object>>(instance.CaseFileJson, JsonOptions) ?? [],
            JsonSerializer.Deserialize<Dictionary<string, string>>(instance.PlanItemStatesJson, JsonOptions) ?? []);
    }

    private async Task PersistAsync(
        CaseInstanceRecord instance,
        Dictionary<string, object> values,
        Dictionary<string, string> states,
        IReadOnlyCollection<string> trace,
        CancellationToken cancellationToken)
    {
        instance.CaseFileJson = JsonSerializer.Serialize(values, JsonOptions);
        instance.PlanItemStatesJson = JsonSerializer.Serialize(states, JsonOptions);
        instance.LastModified = DateTime.UtcNow;
        instance.Revision++;
        db.CmmnHistory.Add(new CmmnHistoryRecord
        {
            Id = Guid.NewGuid(),
            CaseId = instance.Id.ToString(),
            CaseFileJson = instance.CaseFileJson,
            CompletedPlanItemsJson = JsonSerializer.Serialize(states.Where(x => x.Value == "Completed").Select(x => x.Key), JsonOptions),
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeType(string value) => value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    private static void Merge(IDictionary<string, object> target, IReadOnlyDictionary<string, object>? updates)
    {
        if (updates is null) return;
        foreach (var item in updates) target[item.Key] = item.Value;
    }
    private static object? Normalize(object? value) => value is JsonElement element
        ? element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => JsonSerializer.Deserialize<object>(element.GetRawText())
        }
        : value;
}
