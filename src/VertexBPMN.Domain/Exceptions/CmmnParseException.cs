namespace VertexBPMN.Domain.Exceptions;

public class CmmnParseException : Exception
{
    public CmmnParseException(string message) : base(message) { }
    public CmmnParseException(string message, Exception inner) : base(message, inner) { }
}