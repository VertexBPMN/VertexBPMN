namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public interface IExternalConnector
{
    Task<ConnectorResult> ConnectAsync(Dictionary<string, object> connectionParameters);
    Task<ConnectorResult> ExecuteAsync(string operation, Dictionary<string, object> parameters);
    Task DisconnectAsync();
}
