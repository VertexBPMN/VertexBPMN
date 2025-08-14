using System.Collections.Generic;
using System.Threading.Tasks;

namespace VertexBPMN.Core.Engine
{
    public interface IBpmnEngine
    {
        IEnumerable<string> ListProcesses();
        Task<string> StartInstanceAsync(string processKey, Dictionary<string, object> variables);
        Task<object?> GetInstanceStateAsync(string instanceId);
    }
}
