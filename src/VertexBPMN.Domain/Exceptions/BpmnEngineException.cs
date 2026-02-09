namespace VertexBPMN.Domain.Exceptions;

public class BpmnEngineException : Exception
{
    public BpmnEngineException(string message) : base(message) { }
    public BpmnEngineException(string message, Exception inner) : base(message, inner) { }
}