namespace VertexBPMN.Domain.Exceptions;

public class DistributedTokenException : Exception
{
    public DistributedTokenException(string message) : base(message) { }
    public DistributedTokenException(string message, Exception inner) : base(message, inner) { }
}