using VertexBPMN.Domain.Model.Cmmn.InformationModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class CaseParameter : Parameter
{
    public CaseFileItem? BindingRef { get; set; }
}