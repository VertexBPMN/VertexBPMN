using VertexBPMN.Core.Engine;

public interface IProcessEngine
{
    List<string> Execute(BpmnModel model);
}