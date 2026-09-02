namespace VertexBPMN.Domain.Exceptions;

public class ServiceTaskExecutionException : Exception
{
    public ServiceTaskExecutionException(string message) : base(message) { }
    public ServiceTaskExecutionException(string message, Exception inner) : base(message, inner) { }
}