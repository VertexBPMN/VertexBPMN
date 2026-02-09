using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.DRD;

namespace VertexBPMN.Domain.Model.Dmn.Requirements;

public sealed class AuthorityRequirement : DMNElement
{
    public KnowledgeSource? RequiredAuthority { get; set; }
    public Decision? RequiredDecision { get; set; }
    public InputData? RequiredInput { get; set; }
}