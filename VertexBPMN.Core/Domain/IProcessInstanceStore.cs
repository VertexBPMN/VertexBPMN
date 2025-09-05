using VertexBPMN.Core.Cmmn;

namespace VertexBPMN.Core.Domain;

public interface IProcessInstanceStore
{
    // BPMN models and instances
    Task SaveProcessAsync(string key, string bpmnXml);
    Task<string> GetProcessAsync(string key);
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
}