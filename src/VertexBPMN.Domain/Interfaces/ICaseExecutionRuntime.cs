using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public interface ICaseExecutionRuntime
{
    Task<CaseDefinitionRecord> DeployAsync(string key, string name, string cmmnXml, string tenantId, CancellationToken cancellationToken = default);
    Task<CaseDefinitionRecord?> GetDefinitionAsync(string key, string tenantId, CancellationToken cancellationToken = default);
    Task<CaseExecutionResult> StartAsync(string key, string tenantId, IReadOnlyDictionary<string, object>? caseFile = null, CancellationToken cancellationToken = default);
    Task<CaseExecutionResult> CompletePlanItemAsync(Guid caseInstanceId, string planItemId, IReadOnlyDictionary<string, object>? caseFileUpdates = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<CaseExecutionResult> TriggerUserEventAsync(Guid caseInstanceId, string eventId, IReadOnlyDictionary<string, object>? eventData = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<CaseExecutionResult> UpdateCaseFileItemAsync(Guid caseInstanceId, string itemId, object? value, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<CaseExecutionResult> ActivateDiscretionaryItemAsync(Guid caseInstanceId, string planItemId, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<CaseInstanceRecord?> GetInstanceAsync(Guid caseInstanceId, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<CaseInstanceRecord?> ResolveInstanceAsync(string instanceIdOrDefinitionKey, string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CaseHistoryEntry>> GetHistoryAsync(Guid caseInstanceId, string? tenantId = null, CancellationToken cancellationToken = default);
}

public sealed record CaseExecutionResult(CaseInstanceRecord Instance, IReadOnlyList<string> Trace);
public sealed record CaseHistoryEntry(
    Guid CaseInstanceId,
    IReadOnlyDictionary<string, object?> CaseFile,
    IReadOnlyList<string> CompletedPlanItems,
    DateTime Timestamp);
