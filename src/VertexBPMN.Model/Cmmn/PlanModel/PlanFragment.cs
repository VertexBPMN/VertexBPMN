namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Plan fragment (Figure 5.8, inherits from PlanItemDefinition).
/// </summary>
public record PlanFragment(
    List<PlanItem> PlanItems = null!,
    List<Sentry> Sentries = null!
) : PlanItemDefinition(Name: string.Empty);