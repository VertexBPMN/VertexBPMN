namespace VertexBPMN.Domain.Debugging;

public class CallStackFrame
{
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public int Level { get; set; }
}