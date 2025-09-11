namespace VertexBPMN.Core.Modeling;

public record HistoricalCaseData(
    string CaseId,
    Dictionary<string, object> CaseFile,
    List<string> CompletedPlanItems,
    DateTime Timestamp
);