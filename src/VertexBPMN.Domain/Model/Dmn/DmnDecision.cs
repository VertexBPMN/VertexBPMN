namespace VertexBPMN.Domain.Model.Dmn;

public record DmnDecision(
    string Id,
    string Name,
    List<DmnInput> Inputs,
    List<DmnOutput> Outputs,
    List<DmnRule> Rules,
    string HitPolicy = "UNIQUE",
    string? SourceXml = null,
    string? EvaluationTargetId = null);
