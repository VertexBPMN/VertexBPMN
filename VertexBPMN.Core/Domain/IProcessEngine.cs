using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Engine;

namespace VertexBPMN.Core.Domain;

public interface IProcessEngine
{
    List<string> Execute(BpmnModel model);
}