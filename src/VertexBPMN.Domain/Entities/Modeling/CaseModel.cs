namespace VertexBPMN.Domain.Entities.Modeling;

public record CaseModel(
    string Id,
    string Name,
    List<PlanItem> PlanItems,
    List<Sentry> Sentries,
    List<CaseFileItem> CaseFileItems,
    Dictionary<string, string> Attributes = null
);