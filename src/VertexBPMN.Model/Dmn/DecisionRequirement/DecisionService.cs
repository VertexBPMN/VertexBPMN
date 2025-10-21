using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// DecisionService (Figure 6-16, extends Invocable).
/// </summary>
public record DecisionService(
    List<Decision> OutputDecisions,
    List<Decision> EncapsulatedDecisions = null!,
    List<Decision> InputDecisions = null!,
    List<InputData> InputData = null!
) : Invocable(Variable: new InformationItem(TypeRef: string.Empty));