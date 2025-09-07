namespace VertexBPMN.Core.Cmmn;

public record HistoricalCaseData(
    string CaseId,
    Dictionary<string, object> CaseFile,
    List<string> CompletedPlanItems,
    DateTime Timestamp
);