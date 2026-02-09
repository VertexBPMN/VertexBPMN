using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.CaseModel;
using VertexBPMN.Domain.Model.Cmmn.DI;

namespace VertexBPMN.Domain.Model.Cmmn.Core;

public sealed class Definitions : CmmnElement
{
    public string? TargetNamespace { get; set; }
    public Collection<Import> Imports { get; } = new();
    public Collection<InformationModel.CaseFileItemDefinition> CaseFileItemDefinitions { get; } = new();
    public Collection<Extension> Extensions { get; } = new();
    public Collection<Case> Cases { get; } = new();
    public Collection<PlanModel.PlanFragment> PlanFragments { get; } = new();
    public CmmnDi CmmnDi { get; set; }
}