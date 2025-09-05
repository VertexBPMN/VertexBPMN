using VertexBPMN.Core.Bpmn;

namespace VertexBPMN.Core.Domain
{
    //public interface IBpmnParser
    //{
    //    BpmnModel Parse(string bpmnXml);
    //    string Serialize(BpmnModel model);
    //}
    public interface IBpmnParser
    {
        Task<BpmnModel> ParseAsync(string bpmnXml, CancellationToken cancellationToken = default);
        string Serialize(BpmnModel model);
    }
}
