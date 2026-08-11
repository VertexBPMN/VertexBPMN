namespace VertexBPMN.Studio.Services;

public interface ICaseManagementService
{
    Task RegisterModelAsync(string caseId, string cmmnXml, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ExecuteCaseAsync(string caseId, CancellationToken cancellationToken = default);
    Task TriggerUserEventAsync(string caseId, string eventId, IReadOnlyDictionary<string, string> eventData, CancellationToken cancellationToken = default);
    Task UpdateCaseFileItemAsync(string caseId, string itemId, string value, CancellationToken cancellationToken = default);
    Task GenerateAdHocSubprocessAsync(string caseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HistoricalCaseSnapshot>> GetHistoricalContextAsync(string caseId, CancellationToken cancellationToken = default);
}

public sealed record HistoricalCaseSnapshot(
    string CaseId,
    IReadOnlyDictionary<string, string> CaseFile,
    IReadOnlyList<string> CompletedPlanItems,
    DateTime Timestamp);
