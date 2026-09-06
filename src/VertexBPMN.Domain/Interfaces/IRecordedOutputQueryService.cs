using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Resolves the most recent recorded task-IO snapshot output for a service-task
/// element from a previous instance of the same process definition. Used by the
/// CLI test-runner in <c>--use-recorded-outputs</c> mode to replay recorded
/// connector outputs instead of calling a live connector.
/// </summary>
public interface IRecordedOutputQueryService
{
    /// <summary>
    /// Returns the <c>output</c> object of the most recent
    /// <c>TASK_IO_SNAPSHOT</c> <see cref="HistoryEvent"/> for
    /// <paramref name="elementId"/> belonging to a prior instance of the process
    /// definition identified by <paramref name="processDefinitionKey"/> in the
    /// given tenant, or <c>null</c> if none exists.
    /// </summary>
    Task<IReadOnlyDictionary<string, object>?> GetLastRecordedOutputAsync(
        string tenantId,
        string processDefinitionKey,
        string elementId,
        CancellationToken cancellationToken = default);
}
