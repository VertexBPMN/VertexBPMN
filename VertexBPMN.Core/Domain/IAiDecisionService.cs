using VertexBPMN.Core.Cmmn;

namespace VertexBPMN.Core.Domain;

public interface IAiDecisionService
{
    Task<PlanItem> GenerateAdHocSubprocessAsync(string caseId, Dictionary<string, object> caseFile, CancellationToken cancellationToken = default);
    Task<List<PlanItem>> PredictOptimalPlanItemsAsync(string caseId, Dictionary<string, object> caseFile, List<HistoricalCaseData> historicalData, CancellationToken cancellationToken = default);
}
