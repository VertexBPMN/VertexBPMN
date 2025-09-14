using VertexBPMN.Domain.Entities.Modeling;

namespace VertexBPMN.Domain.Interfaces;

public interface IProcessEngine
{
    List<string> Execute(BpmnModel model);
}