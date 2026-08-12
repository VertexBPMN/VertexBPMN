using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.DRD;

namespace VertexBPMN.Domain.Model.Dmn.Requirements;

public sealed class KnowledgeRequirement : DMNElement
{
    public Invocable RequiredKnowledge { get; set; } = default!;
}