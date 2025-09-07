using System.Collections;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Cmmn;
using VertexBPMN.Core.Engine;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Core.Domain;

/// <summary>
/// Distributed token execution engine for enterprise scalability
/// Olympic-level feature: Enterprise Scalability - Distributed processing
/// </summary>
public interface IDistributedTokenEngine
{
    Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default);
    Task<List<string>> ExecuteCaseAsync(CaseModel model, CancellationToken cancellationToken = default);
    Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default);
    Task DistributeTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default);
    Task DistributeCaseTokenAsync(CaseToken token, CancellationToken cancellationToken = default);
    Task<List<ExecutionToken>> GetPendingTokensAsync(CancellationToken cancellationToken = default);
    Task<List<CaseToken>> GetPendingCaseTokensAsync(CancellationToken cancellationToken = default);
    Task RegisterWorkerAsync(WorkerNode worker);
    Task UnregisterWorkerAsync(string workerId);
    Task UpdateWorkerHeartbeatAsync(string workerId);
    Task RegisterDmnModelAsync(string decisionId, string dmnXml);
    Task RegisterCmmnModelAsync(string caseId, string cmmnXml);
    Task AddDiscretionaryItemAsync(string caseId, PlanItem planItem, CancellationToken cancellationToken = default);
    Task UpdateCaseFileItemAsync(string caseId, string caseFileItemId, object newValue, CancellationToken cancellationToken = default);
    Task TriggerUserEventAsync(string caseId, string eventId, Dictionary<string, object> eventData, CancellationToken cancellationToken = default);
    Task GenerateAdHocSubprocessAsync(string caseId, CancellationToken cancellationToken = default);
    Task RegisterBpmnModelAsync(string processId, string bpmnXml, CancellationToken cancellationToken = default);
    Task<List<string>> ExecuteProcessAsync(string processId, CancellationToken cancellationToken = default);
    Task<CaseModel> GetCmmnModelAsync(string caseId);
    Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId);
}