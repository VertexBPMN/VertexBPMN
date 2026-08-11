using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IMessageSignalService
{
    Task<JsonElement> CorrelateMessageAsync(
        string messageName,
        string? processInstanceId = null,
        string? variablesJson = null,
        CancellationToken cancellationToken = default);

    Task<JsonElement> BroadcastSignalAsync(
        string signalName,
        string? variablesJson = null,
        CancellationToken cancellationToken = default);
}
