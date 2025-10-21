namespace VertexBPMN.Domain.Model.Cmmn;

/// <summary>
/// Custom exception for CMMN model-related errors.
/// </summary>
public class CmmnModelException : Exception
{
    public CmmnModelException(string message) : base(message) { }
    public CmmnModelException(string message, Exception inner) : base(message, inner) { }
}