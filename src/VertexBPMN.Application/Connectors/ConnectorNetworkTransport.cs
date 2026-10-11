using System.Net;
using System.Net.Sockets;

namespace VertexBPMN.Application.Connectors;

public sealed class ConnectorNetworkTransport : IConnectorNetworkTransport
{
	public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
		IPAddress.TryParse(host, out var address)
			? Task.FromResult(new[] { address })
			: Dns.GetHostAddressesAsync(host, cancellationToken);

	public async ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
	{
		Socket? socket = new(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		try
		{
			await socket.ConnectAsync(endpoint, cancellationToken);
			var stream = new NetworkStream(socket, ownsSocket: true);
			socket = null; // Ownership has transferred to the returned stream.
			return stream;
		}
		finally
		{
			socket?.Dispose();
		}
	}
}
