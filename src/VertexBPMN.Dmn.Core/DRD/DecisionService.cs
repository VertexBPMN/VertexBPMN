namespace VertexBPMN.Domain.Model.Dmn.DRD;

public sealed class DecisionService : Invocable
{
    public List<Decision> OutputDecisions { get; } = new();
    public List<Decision> EncapsulatedDecisions { get; } = new();
    public List<Decision> InputDecisions { get; } = new();
    public List<InputData> InputData { get; } = new();
}