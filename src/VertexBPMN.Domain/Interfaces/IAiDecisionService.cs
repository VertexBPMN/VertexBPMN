
using VertexBPMN.Domain.Model.Cmn;

namespace VertexBPMN.Domain.Interfaces;

public interface IAiDecisionService
{
    // CMMN AI: Generate dynamic case activities
    Task<PlanItem> GenerateAdHocSubprocessAsync(string caseId, Dictionary<string, object> caseFile, CancellationToken cancellationToken = default);
    
    // Process Mining: Predict optimal next steps
    Task<List<PlanItem>> PredictOptimalPlanItemsAsync(string caseId, Dictionary<string, object> caseFile, List<HistoricalCaseData> historicalData, CancellationToken cancellationToken = default);
    
    // Context Enrichment: External data integration
    Task<Dictionary<string, object>> FetchExternalContextAsync(string caseId, string resourceId, CancellationToken cancellationToken = default);
    
    // MCP Protocol: Execute AI actions via external services
    Task ExecuteMcpActionAsync(string caseId, string mcpServerUrl, string method, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}
