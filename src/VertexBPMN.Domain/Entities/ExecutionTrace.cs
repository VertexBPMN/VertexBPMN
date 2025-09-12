using System.Collections.Generic;

namespace VertexBPMN.Domain;

public class ExecutionTrace
{
    public List<string> ExecutedNodes { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}