using VertexBPMN.Domain.Model.Bpmn.Model;

namespace VertexBPMN.Engine;

public sealed class EngineMapper
{
    public EngineDeploymentResult Map(string processKey, BpmnModel model)
    {
        var diagnostics = new List<string>();
        if (!string.Equals(model.ProcessId, processKey, StringComparison.Ordinal))
            diagnostics.Add($"Process key mismatch: model={model.ProcessId} expected={processKey}");

        var nodeDict = new Dictionary<string, EngineFlowNode>(StringComparer.Ordinal);
        void AddNode(string id,string type,string? sub,bool gateway=false,bool evt=false,bool task=false,bool subProc=false)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!nodeDict.TryAdd(id, new EngineFlowNode(id,type,sub,gateway,evt,task,subProc, type == "endEvent", type == "userTask")))
                diagnostics.Add($"Duplicate flow node id during mapping: {id}");
        }

        foreach (var e in model.Events) AddNode(e.Id,e.Type,e.SubprocessId,evt:true);
        foreach (var g in model.Gateways) AddNode(g.Id,g.Type,g.SubprocessId,gateway:true);
        foreach (var t in model.Tasks) AddNode(t.Id,t.Type,t.SubprocessId,task:true);
        foreach (var sp in model.Subprocesses) AddNode(sp.Id,"subProcess",sp.SubprocessId,subProc:true);

        var flows = model.SequenceFlows.Select(f => new EngineSequenceFlow(
            f.Id,f.SourceRef,f.TargetRef,f.IsDefault,f.ConditionExpression,f.Priority)).ToList();

        // Adjacency
        var outgoing = flows.GroupBy(f=>f.SourceId)
            .ToDictionary(g=>g.Key,g=> (IReadOnlyList<EngineSequenceFlow>)g.OrderByDescending(x=>x.Priority ?? int.MinValue).ToList());
        var incoming = flows.GroupBy(f=>f.TargetId)
            .ToDictionary(g=>g.Key,g=> (IReadOnlyList<EngineSequenceFlow>)g.ToList());

        var startEventIds = model.Events.Where(e=> e.Type=="startEvent" && e.SubprocessId==null).Select(e=>e.Id).ToList();
        if (startEventIds.Count==0) diagnostics.Add("No top-level start event mapped");

        // Combine diagnostics with model diagnostics at the end
        var combinedDiags = diagnostics.Concat(model.Diagnostics).ToList();
        var def = new EngineProcessDefinition(processKey, nodeDict, flows, startEventIds, outgoing, incoming, DateTime.UtcNow, combinedDiags);
        return new EngineDeploymentResult(def, combinedDiags);
    }
}
