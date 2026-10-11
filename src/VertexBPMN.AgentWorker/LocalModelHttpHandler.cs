using System.Net;
using System.Net.Sockets;

namespace VertexBPMN.AgentWorker;
internal static class LocalModelHttpHandler
{
	public static HttpMessageHandler Create() => new SocketsHttpHandler
	{
		AllowAutoRedirect = false,
		ConnectCallback = ConnectLoopbackAsync
	};
	private static async ValueTask<Stream> ConnectLoopbackAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
	{
		var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
		if (addresses.Length == 0 || addresses.Any(address => !IPAddress.IsLoopback(address)))
		{
			throw new HttpRequestException("Local model endpoint resolved outside loopback.");
		}

		Exception? failure = null;
		foreach (var address in addresses)
		{
			Socket? socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			try
			{
				await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
				var stream = new NetworkStream(socket, ownsSocket: true);
				socket = null; // The returned stream owns the connected socket.
				return stream;
			}
			catch (SocketException exception)
			{
				failure = exception;
			}
			finally
			{
				socket?.Dispose();
			}
		}

		throw new HttpRequestException("Local model endpoint is unavailable.", failure);
	}
}
