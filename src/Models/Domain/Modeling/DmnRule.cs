using System.Collections.Generic;

namespace VertexBPMN.Domain.Modeling;

public record DmnRule(string Id, IReadOnlyDictionary<string, string> InputConditions, IReadOnlyDictionary<string, object> OutputValues);