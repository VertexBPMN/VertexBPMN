namespace VertexBPMN.Domain.Exceptions;

public class InvalidIoMappingException : Exception
{
	public InvalidIoMappingException(string taskDefinitionType, string taskId)
		: base($"No IoMapping found for taskDefinition '{taskDefinitionType}' in ServiceTask '{taskId}'.")
	{
	}

	public InvalidIoMappingException()
	{
	}

	public InvalidIoMappingException(string? message) : base(message)
	{
	}

	public InvalidIoMappingException(string? message, Exception? innerException) : base(message, innerException)
	{
	}
}
