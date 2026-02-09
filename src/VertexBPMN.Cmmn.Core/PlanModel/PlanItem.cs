using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class PlanItem : CmmnElement
{
    public PlanItemDefinition? DefinitionRef { get; set; }
    public ItemControl? ItemControl { get; set; }
    public Collection<EntryCriterion> EntryCriteria { get; } = new();
    public Collection<ExitCriterion> ExitCriteria { get; } = new();
}