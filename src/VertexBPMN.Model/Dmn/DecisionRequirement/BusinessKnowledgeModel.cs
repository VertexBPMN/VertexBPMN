using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.Expression;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// BusinessKnowledgeModel (Figure 6-15, extends Invocable).
/// </summary>
public record BusinessKnowledgeModel(
    FunctionDefinition? EncapsulatedLogic = null,
    List<KnowledgeRequirement> KnowledgeRequirements = null!,
    List<AuthorityRequirement> AuthorityRequirements = null!
) : Invocable(Variable: new InformationItem(TypeRef: string.Empty));