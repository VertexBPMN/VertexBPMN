namespace VertexBPMN.Domain.Exceptions;

public class BpmnParseException : Exception
{
    public BpmnParseException(string message) : base(message) { }
    public BpmnParseException(string message, Exception inner) : base(message, inner) { }
}