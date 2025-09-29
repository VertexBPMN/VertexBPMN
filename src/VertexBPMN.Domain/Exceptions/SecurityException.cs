namespace VertexBPMN.Domain.Exceptions;

/// <summary>
/// Security-related exception for parsing operations.
/// </summary>
public sealed class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
    public SecurityException(string message, Exception innerException) : base(message, innerException) { }
}