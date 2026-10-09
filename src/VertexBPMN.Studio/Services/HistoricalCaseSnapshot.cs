namespace VertexBPMN.Studio.Services;

internal sealed record HistoricalCaseSnapshot(
    string CaseId,
    IReadOnlyDictionary<string, string> CaseFile,
    IReadOnlyList<string> CompletedPlanItems,
    DateTime Timestamp);
