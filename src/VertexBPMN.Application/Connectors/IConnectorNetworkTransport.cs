using System.Net;
using System.Net.Sockets;

namespace VertexBPMN.Application.Connectors;

/// <summary>Network IO boundary. Destination validation remains in the HTTP handler.</summary>
public interface IConnectorNetworkTransport
{
	Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
	ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken);
}
