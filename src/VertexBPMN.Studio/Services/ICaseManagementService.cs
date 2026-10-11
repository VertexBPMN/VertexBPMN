namespace VertexBPMN.Studio.Services;

internal interface ICaseManagementService
{
    Task RegisterModelAsync(string caseId, string cmmnXml, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ExecuteCaseAsync(string caseId, CancellationToken cancellationToken = default);
    Task TriggerUserEventAsync(string caseId, string eventId, IReadOnlyDictionary<string, string> eventData, CancellationToken cancellationToken = default);
    Task UpdateCaseFileItemAsync(string caseId, string itemId, string value, CancellationToken cancellationToken = default);
    Task GenerateAdHocSubprocessAsync(string caseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HistoricalCaseSnapshot>> GetHistoricalContextAsync(string caseId, CancellationToken cancellationToken = default);
}
