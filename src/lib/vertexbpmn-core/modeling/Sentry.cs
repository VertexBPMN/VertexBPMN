namespace VertexBPMN.Core.Cmmn;

public record Sentry(
    string Id,
    List<SentryCondition> Conditions,// Erweitert um IfPart/OnPart
    string OnPartRef, // Referenz auf PlanItem/Event
    bool IsEntryCriterion
);