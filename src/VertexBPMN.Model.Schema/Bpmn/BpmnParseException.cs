namespace VertexBPMN.Domain.Model.Bpmn;

#nullable enable

/// <summary>
/// Custom exception for BPMN model-related errors.
/// </summary>
public class BpmnParseException : Exception
{
    public BpmnParseException(string message) : base(message) { }
    public BpmnParseException(string message, Exception inner) : base(message, inner) { }
}

