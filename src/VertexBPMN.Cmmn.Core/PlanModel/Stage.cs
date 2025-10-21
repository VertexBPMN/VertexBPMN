using System.Collections.ObjectModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public class Stage : PlanItemDefinition
{
    public Collection<PlanItem> PlanItems { get; } = new();
    public Collection<DiscretionaryItem> DiscretionaryItems { get; } = new();
    public PlanningTable? PlanningTable { get; set; }
}