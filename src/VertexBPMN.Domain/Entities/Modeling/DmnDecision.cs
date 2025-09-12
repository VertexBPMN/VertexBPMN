using System.Collections.Generic;

namespace VertexBPMN.Domain.Modeling;

public record DmnDecision(string Id, string Name, List<DmnInput> Inputs, List<DmnOutput> Outputs, List<DmnRule> Rules, string HitPolicy = "UNIQUE");