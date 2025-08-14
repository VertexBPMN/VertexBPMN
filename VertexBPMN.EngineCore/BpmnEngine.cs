using System.Collections.Generic;
using System.Threading.Tasks;

namespace VertexBPMN.Core;

public interface IBpmnEngine
{
    List<string> ListProcesses();
    Task<string> StartInstanceAsync(string processKey, Dictionary<string, object> variables);
    Task<object> GetInstanceStateAsync(string instanceId);
}

public class BpmnEngine : IBpmnEngine
{
    private readonly List<string> _processes = new() { "invoice", "order", "approval" };
    private readonly Dictionary<string, object> _instances = new();

    public List<string> ListProcesses() => _processes;

    public async Task<string> StartInstanceAsync(string processKey, Dictionary<string, object> variables)
    {
        var instanceId = System.Guid.NewGuid().ToString();
        _instances[instanceId] = new { processKey, variables, state = "running" };
        await Task.Delay(100); // Simulate async
        return instanceId;
    }

    public async Task<object> GetInstanceStateAsync(string instanceId)
    {
        await Task.Delay(50);
        return _instances.TryGetValue(instanceId, out var state) ? state : new { error = "not found" };
    }
}
