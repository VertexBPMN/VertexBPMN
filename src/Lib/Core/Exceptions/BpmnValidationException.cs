namespace VertexBPMN.Core.Exceptions;

public class BpmnValidationException : Exception
{
    public List<string> Errors { get; }
    public BpmnValidationException(string message, List<string> errors) : base(message)
    {
        Errors = errors;
    }
}