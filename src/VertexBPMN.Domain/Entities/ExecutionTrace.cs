namespace VertexBPMN.Domain.Entities;

public class ExecutionTrace
{
    public List<string> ExecutedNodes { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}