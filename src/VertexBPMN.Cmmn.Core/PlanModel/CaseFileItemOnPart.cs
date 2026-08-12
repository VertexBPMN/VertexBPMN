using VertexBPMN.Domain.Model.Cmmn.InformationModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class CaseFileItemOnPart : OnPart
{
    public CaseFileItem? SourceRef { get; set; }
    public string? Transition { get; set; }
}