using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Model.Cmn;

namespace VertexBPMN.Domain.Interfaces;

public interface IProcessInstanceStore
{
    // BPMN models and instances
    Task SaveBpmnModelAsync(string processId, string bpmnXml);
    Task<string> GetBpmnModelAsync(string processId);
    Task<IEnumerable<string>> ListProcessesAsync();
    Task SaveInstanceAsync(ProcessInstance instance);
    Task<ProcessInstance> GetInstanceAsync(string instanceId);

    // Tokens and workers
    Task SaveTokenAsync(ExecutionToken token);
    Task<ExecutionToken> GetTokenAsync(Guid tokenId);
    Task<List<ExecutionToken>> GetPendingTokensAsync();
    Task SaveWorkerAsync(WorkerNode worker);
    Task<WorkerNode> GetWorkerAsync(string workerId);
    Task<List<WorkerNode>> GetActiveWorkersAsync();
    Task RemoveWorkerAsync(string workerId);
    Task SaveToDeadLetterQueueAsync<T>(T token, string errorMessage);

    // DMN
    Task SaveDmnModelAsync(string decisionId, string dmnXml);
    Task<string> GetDmnModelAsync(string decisionId, CancellationToken cancellationToken = default);

    // CMMN
    Task SaveCaseTokenAsync(CaseToken token);
    Task<CaseToken> GetCaseTokenAsync(Guid tokenId);
    Task<List<CaseToken>> GetPendingCaseTokensAsync();
    Task SaveCmmnModelAsync(string caseId, string cmmnXml);
    Task<string> GetCmmnModelAsync(string caseId);
    Task UpdateCaseModelAsync(CaseModel model);
    Task SaveHistoricalCaseDataAsync(HistoricalCaseData data);
    Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId);
}