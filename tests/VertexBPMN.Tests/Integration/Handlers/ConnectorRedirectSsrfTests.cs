using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.Extensions;

namespace VertexBPMN.Tests.Integration.Handlers;

/// <summary>
/// M1 (Phase 3): Der geteilte Connector-<see cref="HttpClient"/> verfolgt keine
/// automatischen Redirects — ein 3xx-<c>Location</c> kann den ConnectorDestination-
/// SSRF-Guard nicht umgehen (Redirect zurück auf eine interne Adresse wird NICHT
/// gefolgt).
/// </summary>
public sealed class ConnectorRedirectSsrfTests
{
    [Fact]
    public void RegisteredConnectorHandler_DoesNotAutoFollowRedirects()
    {
        var services = new ServiceCollection();
        ServiceTaskRegistryExtensions.AddServiceTaskHandlers(services, null);
        using var provider = services.BuildServiceProvider();

        var handler = provider.GetRequiredService<SocketsHttpHandler>();

        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public async Task ConnectorHttpClient_ReturnsRedirect_InsteadOfFollowingToInternalTarget()
    {
        using var listener = RedirectingServer.Start();

        // The production handler disables auto-redirect (asserted above) but also rejects
        // loopback at connect (M5). To exercise the redirect behavior in isolation against a
        // local redirect server, mirror the production AllowAutoRedirect setting on a plain
        // handler without the M5 ConnectCallback.
        using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
        var response = await client.GetAsync($"{listener.BaseUrl}/redirect");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); // 302, not followed
        Assert.False(listener.InternalTargetHit, "Redirect to an internal address must not be followed.");
    }

    private sealed class RedirectingServer : IDisposable
    {
        private readonly HttpListener _listener;
        public string BaseUrl { get; }
        public bool InternalTargetHit { get; private set; }

        private RedirectingServer(int port)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            BaseUrl = $"http://127.0.0.1:{port}";
            _ = Task.Run(ServeAsync);
        }

        public static RedirectingServer Start()
        {
            var port = GetFreePort();
            return new RedirectingServer(port);
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    return; // listener closed
                }

                var path = ctx.Request.Url!.AbsolutePath;
                if (path == "/redirect")
                {
                    ctx.Response.StatusCode = 302;
                    ctx.Response.RedirectLocation = $"{BaseUrl}/internal";
                }
                else if (path == "/internal")
                {
                    InternalTargetHit = true;
                    ctx.Response.StatusCode = 200;
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
                ctx.Response.Close();
            }
        }

        private static int GetFreePort()
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            _listener.Close();
        }
    }
}
