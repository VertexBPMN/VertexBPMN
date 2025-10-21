using VertexBPMN.Domain.Model.Dmn.Requirements;

namespace VertexBPMN.Domain.Model.Dmn.DRD;

public sealed class BusinessKnowledgeModel : Invocable
{
    public Expressions.FunctionDefinition? EncapsulatedLogic { get; set; }
    public List<KnowledgeRequirement> KnowledgeRequirements { get; } = new();
    public List<AuthorityRequirement> AuthorityRequirements { get; } = new();
}