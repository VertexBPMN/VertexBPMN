namespace VertexBPMN.Api.Plugins;

public interface IExternalConnector
{
    Task<ConnectorResult> ConnectAsync(Dictionary<string, object> connectionParameters);
    Task<ConnectorResult> ExecuteAsync(string operation, Dictionary<string, object> parameters);
    Task DisconnectAsync();
}