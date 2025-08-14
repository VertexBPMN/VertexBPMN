using System.Collections.Generic;
using System.Threading.Tasks;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Tasks;

namespace VertexBPMN.Core.Engine
{
    public class BpmnEngine : IBpmnEngine
    {
        private readonly IBpmnParser _parser;
        private readonly IProcessEngine _engine;
        // For distributed: private readonly DistributedTokenEngine _distributedEngine;
        // Demo: In-memory process registry
        private readonly Dictionary<string, string> _processRegistry = new(); // key -> BPMN XML
        private readonly Dictionary<string, object> _instanceStates = new(); // instanceId -> state
       
        public BpmnEngine(IBpmnParser parser, IProcessEngine engine)
        {
            _parser = parser;
            _engine = engine;
        }

        public IEnumerable<string> ListProcesses()
        {
            return _processRegistry.Keys;
        }

        public async Task<string> StartInstanceAsync(string processKey, Dictionary<string, object> variables)
        {
            if (!_processRegistry.TryGetValue(processKey, out var bpmnXml))
                throw new KeyNotFoundException($"Process {processKey} not found.");
            var model = _parser.Parse(bpmnXml);
            var trace = _engine.Execute(model);
            var instanceId = Guid.NewGuid().ToString();
            _instanceStates[instanceId] = new { state = "completed", trace };
            return await Task.FromResult(instanceId);
        }

        public async Task<object?> GetInstanceStateAsync(string instanceId)
        {
            _instanceStates.TryGetValue(instanceId, out var state);
            return await Task.FromResult(state);
        }

        // Demo: Add process to registry
        public void RegisterProcess(string key, string bpmnXml)
        {
            _processRegistry[key] = bpmnXml;
        }
    }
}
