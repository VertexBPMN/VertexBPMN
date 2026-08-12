namespace VertexBPMN.Domain.Entities.Modeling;

public record HistoricalCaseData(
    string CaseId,
    Dictionary<string, object> CaseFile,
    List<string> CompletedPlanItems,
    DateTime Timestamp
);