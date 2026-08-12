namespace VertexBPMN.Domain.Model.Dmn;

/// <summary>
/// Custom exception for DMN model-related errors.
/// </summary>
public class DmnModelException : Exception
{
    public DmnModelException(string message) : base(message) { }
    public DmnModelException(string message, Exception inner) : base(message, inner) { }
}