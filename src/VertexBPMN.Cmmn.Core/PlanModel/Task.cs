using System.Collections.ObjectModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public abstract class Task : PlanItemDefinition
{
    public bool IsBlocking { get; set; } = true;
    public Collection<CaseParameter> Inputs { get; } = new();
    public Collection<CaseParameter> Outputs { get; } = new();
}