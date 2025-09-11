using VertexBPMN.Core.Modeling;

namespace VertexBPMN.Core.Contracts;

public interface IProcessEngine
{
    List<string> Execute(BpmnModel model);
}