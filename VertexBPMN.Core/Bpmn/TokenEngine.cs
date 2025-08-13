using System.Collections.Generic;

namespace VertexBPMN.Core.Bpmn;

/// <summary>
/// BPMN-Token-Engine: Führt einen minimalen Token-Flow für Start/End-Events und Sequence Flows aus.
/// </summary>
public class TokenEngine
{
    public List<string> Execute(BpmnModel model, VertexBPMN.Core.Services.IDecisionService? decisionService = null)
    {
        var trace = new List<string>();
        var start = model.Events.FirstOrDefault(e => e.Type == "startEvent");
        if (start == null) throw new InvalidOperationException("No startEvent found.");
        trace.Add($"StartEvent: {start.Id}");
        var currentId = start.Id;
        while (true)
        {
            var flows = model.SequenceFlows.Where(f => f.SourceRef == currentId).ToList();
            if (flows.Count == 0) break;
            // ParallelGateway: alle ausgehenden Flows
            var gateway = model.Gateways.FirstOrDefault(g => g.Id == currentId);
            if (gateway != null && gateway.Type == "parallelGateway")
            {
                trace.Add($"ParallelGateway: {gateway.Id}");
                foreach (var flow in flows)
                {
                    trace.Add($"SequenceFlow: {flow.Id}");
                    var target = flow.TargetRef;
                    trace.Add($"ParallelBranch: {target}");
                }
                break;
            }
            // InclusiveGateway: alle ausgehenden Flows (wie Parallel, aber semantisch anders)
            if (gateway != null && gateway.Type == "inclusiveGateway")
            {
                trace.Add($"InclusiveGateway: {gateway.Id}");
                foreach (var flow in flows)
                {
                    trace.Add($"SequenceFlow: {flow.Id}");
                    var target = flow.TargetRef;
                    trace.Add($"InclusiveBranch: {target}");
                }
                break;
            }
            // ExclusiveGateway: nur erster Flow
            if (gateway != null && gateway.Type == "exclusiveGateway")
            {
                trace.Add($"ExclusiveGateway: {gateway.Id}");
                var flow = flows.First();
                trace.Add($"SequenceFlow: {flow.Id}");
                currentId = flow.TargetRef;
                continue;
            }
            // Subprocess
            var subprocess = model.Subprocesses.FirstOrDefault(s => s.Id == currentId);
            if (subprocess != null)
            {
                if (subprocess.IsTransaction)
                {
                    trace.Add($"TransactionSubprocess: {subprocess.Id}");
                }
                else
                {
                    trace.Add($"Subprocess: {subprocess.Id}");
                }
                if (subprocess.IsMultiInstance)
                {
                    trace.Add($"MultiInstance: {subprocess.Id}");
                }
                // Simuliere: Subprozess hat einen StartEvent mit gleicher Id + "_start"
                trace.Add($"SubprocessStart: {subprocess.Id}_start");
                // Kompensationshandler prüfen
                var compensation = model.Events.FirstOrDefault(e => e.Type == "boundaryEvent" && e.AttachedToRef == subprocess.Id && e.IsCompensation);
                if (compensation != null)
                {
                    trace.Add($"CompensationHandler: {compensation.Id}");
                }
                // Simuliere: Subprozess-Ende
                trace.Add($"SubprocessEnd: {subprocess.Id}_end");
                // Weiter zum nächsten Flow
                var flow = flows.FirstOrDefault();
                if (flow == null) break;
                trace.Add($"SequenceFlow: {flow.Id}");
                // Prüfe, ob das Ziel ein EndEvent ist
                var endEvt = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef && e.Type == "endEvent");
                if (endEvt != null)
                {
                    trace.Add($"EndEvent: {endEvt.Id}");
                    break;
                }
                currentId = flow.TargetRef;
                continue;
            }
            // UserTask
            var task = model.Tasks.FirstOrDefault(t => t.Id == currentId);
            if (task != null)
            {
                if (task.Type == "businessRuleTask" && decisionService != null)
                {
                    trace.Add($"BusinessRuleTask: {task.Id}");
                    // Simuliere DMN-Auswertung
                    var result = decisionService.EvaluateDecisionByKeyAsync(task.Id, new Dictionary<string, object> { { "input", 1 } }).Result;
                    trace.Add($"DecisionEvaluated: {task.Id} => {result.Outputs["input"]}");
                }
                else
                {
                    trace.Add($"UserTask: {task.Id}");
                }
                // Weiter zum nächsten Flow
                var flow = flows.FirstOrDefault();
                if (flow == null) break;
                trace.Add($"SequenceFlow: {flow.Id}");
                // Prüfe, ob das Ziel ein EndEvent ist
                var endEvt = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef && e.Type == "endEvent");
                if (endEvt != null)
                {
                    trace.Add($"EndEvent: {endEvt.Id}");
                    break;
                }
                currentId = flow.TargetRef;
                continue;
            }
            // Default: nur erster Flow
            var defaultFlow = flows.First();
            trace.Add($"SequenceFlow: {defaultFlow.Id}");
            currentId = defaultFlow.TargetRef;
            var evt = model.Events.FirstOrDefault(e => e.Id == currentId);
            if (evt != null && evt.Type == "endEvent")
            {
                trace.Add($"EndEvent: {evt.Id}");
                break;
            }
        }
        return trace;
    }
}
