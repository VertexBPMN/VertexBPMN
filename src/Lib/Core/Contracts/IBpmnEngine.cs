using VertexBPMN.Domain;

namespace VertexBPMN.Core.Contracts
{
    public interface IBpmnEngine
    {
        Task<IEnumerable<string>> ListProcessesAsync();
        Task<string> StartInstanceAsync(string processKey, Dictionary<string, object> variables);
        Task<ProcessInstance> GetInstanceStateAsync(string instanceId);
        Task RegisterProcessAsync(string key, string bpmnXml);
        Task CompleteTaskAsync(string instanceId, string taskId, Dictionary<string, object> variables);
    }
}
