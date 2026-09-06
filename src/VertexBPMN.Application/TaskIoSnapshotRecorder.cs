using System.Runtime.CompilerServices;

namespace VertexBPMN.Application;

/// <summary>
/// Records a redacted input/output snapshot for a service-task execution, gated
/// behind a feature flag. Implemented in Infrastructure (writes <see cref="HistoryEvent"/>).
/// </summary>
public interface ITaskIoSnapshotRecorder
{
    Task RecordAsync(
        Guid processInstanceId,
        string elementId,
        string tenantId,
        IReadOnlyDictionary<string, object> input,
        IReadOnlyDictionary<string, object>? output,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default);
}
