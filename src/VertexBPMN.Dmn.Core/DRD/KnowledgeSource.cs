using VertexBPMN.Domain.Model.Dmn.BusinessContext;
using VertexBPMN.Domain.Model.Dmn.Requirements;

namespace VertexBPMN.Domain.Model.Dmn.DRD;

public sealed class KnowledgeSource : DRGElement
{
    public Uri? LocationURI { get; set; }
    public string? Type { get; set; }
    public OrganisationalUnit? Owner { get; set; }
    public List<AuthorityRequirement> AuthorityRequirements { get; } = new();
}