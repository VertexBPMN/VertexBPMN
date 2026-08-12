using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Faults;

public class Error : RootElement
{
    public string? Name { get; set; }
    public string? ErrorCode { get; set; }
    public string? StructureRef { get; set; }
}