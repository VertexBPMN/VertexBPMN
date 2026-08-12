using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public class PlanItemControl : CmmnElement
{
    public ManualActivationRule? ManualActivationRule { get; set; }
    public RequiredRule? RequiredRule { get; set; }
    public RepetitionRule? RepetitionRule { get; set; }
}