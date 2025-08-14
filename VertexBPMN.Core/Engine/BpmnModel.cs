using System.Linq;
using VertexBPMN.Core.Bpmn;

namespace VertexBPMN.Core.Engine;

public partial record BpmnModel(
    string Id,
    string Name,
    List<BpmnEvent> Events,
    List<BpmnTask> Tasks,
    List<BpmnGateway> Gateways,
    List<BpmnSubprocess> Subprocesses,
    List<BpmnSequenceFlow> SequenceFlows,
    IDictionary<string, object>? ProcessVariables = null
);

public partial record BpmnModel
{
    public string ProcessId => Id;
    public IEnumerable<object> Activities => Tasks.Cast<object>().Concat(Subprocesses);
}