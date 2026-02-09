namespace VertexBPMN.Domain.Model.Cmn;

public record Sentry(
    string Id,
    List<SentryCondition> Conditions,// Erweitert um IfPart/OnPart
    string OnPartRef, // Referenz auf PlanItem/Event
    bool IsEntryCriterion
);