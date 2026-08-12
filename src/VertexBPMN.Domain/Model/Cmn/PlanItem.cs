namespace VertexBPMN.Domain.Model.Cmn;

public record PlanItem(
    string Id,
    string Type, // task, stage, milestone, eventListener
    string DefinitionRef, // Referenz auf Task/Stage/Milestone
    Dictionary<string, string> Attributes = null,
    List<string> EntrySentryRefs = null,
    List<string> ExitSentryRefs = null,
    bool IsDiscretionary = false
);