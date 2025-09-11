namespace VertexBPMN.Core.Dmn;

public record DmnRule(string Id, IReadOnlyDictionary<string, string> InputConditions, IReadOnlyDictionary<string, object> OutputValues);