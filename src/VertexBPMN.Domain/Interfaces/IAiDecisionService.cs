using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Modeling;
using VertexBPMN.Domain.Modeling;

namespace VertexBPMN.Domain.Contracts;

public interface IAiDecisionService
{
    Task<PlanItem> GenerateAdHocSubprocessAsync(string caseId, Dictionary<string, object> caseFile, CancellationToken cancellationToken = default);
    Task<List<PlanItem>> PredictOptimalPlanItemsAsync(string caseId, Dictionary<string, object> caseFile, List<HistoricalCaseData> historicalData, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> FetchExternalContextAsync(string caseId, string resourceId, CancellationToken cancellationToken = default);
    Task ExecuteMcpActionAsync(string caseId, string mcpServerUrl, string method, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}
