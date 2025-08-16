namespace VertexBPMN.Core.Exceptions;

public class TaskHandlerNotFoundException : Exception
{
    public TaskHandlerNotFoundException(string taskDefinitionType, string taskId)
        : base($"No handler found for taskDefinition '{taskDefinitionType}' in ServiceTask '{taskId}'.")
    {
    }
}

public class InvalidIoMappingException : Exception
{
    public InvalidIoMappingException(string taskDefinitionType, string taskId)
        : base($"No IoMapping found for taskDefinition '{taskDefinitionType}' in ServiceTask '{taskId}'.")
    {
    }
}