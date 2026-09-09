using System.Net;
using System.Net.Sockets;

namespace VertexBPMN.Application.Connectors;

/// <summary>Network IO boundary. Destination validation remains in the HTTP handler.</summary>
public interface IConnectorNetworkTransport
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
    ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken);
}

public sealed class ConnectorNetworkTransport : IConnectorNetworkTransport
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        IPAddress.TryParse(host, out var address)
            ? Task.FromResult(new[] { address })
            : Dns.GetHostAddressesAsync(host, cancellationToken);

    public async ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
