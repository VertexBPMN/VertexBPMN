namespace VertexBPMN.Core.Engine
{
    public interface IBpmnParser
    {
        BpmnModel Parse(string bpmnXml);
        string Serialize(BpmnModel model);
    }
}
