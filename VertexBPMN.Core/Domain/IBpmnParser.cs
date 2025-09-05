using VertexBPMN.Core.Bpmn;

namespace VertexBPMN.Core.Domain
{
    public interface IBpmnParser
    {
        Task<BpmnModel> ParseAsync(string bpmnXml, CancellationToken cancellationToken = default);
        string Serialize(BpmnModel model);
    }
}
