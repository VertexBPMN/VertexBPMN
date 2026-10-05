using System.Net;
using System.Net.Sockets;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Direct TLS with DNS-to-socket pinning, no proxy, redirects or ambient credentials.</summary>
public static class SourceControlHttps
{
    public static void ValidateTarget(Uri target, IReadOnlyList<string> allowedHosts)
    {
        if (!target.IsAbsoluteUri || target.Scheme != "https" || target.Port != 443
            || target.UserInfo.Length != 0 || target.Query.Length != 0 || target.Fragment.Length != 0
            || target.HostNameType != UriHostNameType.Dns || target.Host.EndsWith('.')
            || !allowedHosts.Contains(target.IdnHost, StringComparer.OrdinalIgnoreCase)) Reject();
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) return IsPublicAddress(address.MapToIPv4());
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return b[0] is not (0 or 10 or 127) && b[0] < 224
                && !(b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                && !(b[0] == 169 && b[1] == 254)
                && !(b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                && !(b[0] == 192 && (b[1] == 168 || b[1] == 0 || b[1] == 2))
                && !(b[0] == 198 && (b[1] == 18 || b[1] == 19 || b[1] == 51 && b[2] == 100))
                && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
        // Conservative IPv6 global-unicast policy, excluding documentation and protocol assignments.
        return address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId == 0
            && (b[0] & 0xe0) == 0x20
            && !(b[0] == 0x20 && b[1] == 0x01 && (b[2] < 2 || b[2] == 0x0d && b[3] == 0xb8))
            && !(b[0] == 0x20 && b[1] == 0x02)
            && !(b[0] == 0x3f && b[1] == 0xff);
    }

    public static HttpClient CreateClient(IReadOnlyList<string> allowedHosts, TimeSpan timeout)
        => CreateClient(allowedHosts, timeout, Dns.GetHostAddressesAsync);

    internal static HttpClient CreateClient(IReadOnlyList<string> allowedHosts, TimeSpan timeout,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve)
    {
        var hosts = allowedHosts.ToArray();
        var handler = new SocketsHttpHandler
        {
            UseProxy = false, AllowAutoRedirect = false, UseCookies = false,
            Credentials = null, ConnectTimeout = timeout, MaxResponseHeadersLength = 32,
            ConnectCallback = async (context, cancellation) =>
            {
                ValidateTarget(context.InitialRequestMessage.RequestUri!, hosts);
                if (context.DnsEndPoint.Port != 443) Reject();
                var addresses = await resolve(context.DnsEndPoint.Host, cancellation);
                if (addresses.Length == 0 || addresses.Any(a => !IsPublicAddress(a))) Reject();
                foreach (var address in addresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        // No second DNS lookup: connect only to the already approved address.
                        await socket.ConnectAsync(new IPEndPoint(address, 443), cancellation);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (SocketException) { socket.Dispose(); }
                    catch { socket.Dispose(); throw; }
                }
                throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
            }
        };
        return new HttpClient(new TargetGuard(handler, hosts)) { Timeout = timeout };
    }

    private sealed class TargetGuard(HttpMessageHandler inner, string[] hosts) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ValidateTarget(request.RequestUri!, hosts);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private static void Reject() => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
}
