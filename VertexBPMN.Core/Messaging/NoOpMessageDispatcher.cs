namespace VertexBPMN.Core.Messaging;

public class NoOpMessageDispatcher : IMessageDispatcher
{  
    public Task DispatchServiceTaskAsync(string targetWorkerId, string implementation, IDictionary<string, string> attributes,
        IDictionary<string, object> variables, CancellationToken ct = default)
    {
        // Macht nichts
        return Task.CompletedTask;
    }
}