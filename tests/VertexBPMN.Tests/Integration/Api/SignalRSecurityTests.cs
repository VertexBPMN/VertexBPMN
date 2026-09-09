using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using VertexBPMN.Sdk;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class SignalRSecurityTests
{
    [Theory]
    [InlineData("monitoring-hub")]
    [InlineData("debug-hub")]
    public async Task HubSubscriptions_EnforceProcessAndTenantOwnership(string hub)
    {
        using var root = new CustomWebApplicationFactory();
        using var host = root.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["PathBase"] = "" })));
        using var http = host.CreateClient();
        var sdk = new VertexBpmnClient(http);
        var ct = TestContext.Current.CancellationToken;
        var key = "signalr-" + Guid.NewGuid().ToString("N");
        await sdk.DeployProcessAsync($"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='s'/><sequenceFlow id='f' sourceRef='s' targetRef='wait'/><userTask id='wait'/></process></definitions>", key, "tenant-a", ct);
        var instance = await sdk.StartProcessAsync(key, tenantId: "tenant-a", cancellationToken: ct);
        Assert.NotNull(instance);

        HubConnection Connect(string tenant) => new HubConnectionBuilder().WithUrl($"http://localhost/api/{hub}", options =>
        {
            options.HttpMessageHandlerFactory = _ => host.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
            options.Headers["X-Test-User"] = "reader";
            options.Headers["X-Test-Tenant"] = tenant;
        }).Build();

        await using var own = Connect("tenant-a");
        await own.StartAsync(ct);
        await own.InvokeAsync("JoinProcessGroup", instance.Id.ToString(), ct);
        await using var foreign = Connect("tenant-b");
        await foreign.StartAsync(ct);
        await Assert.ThrowsAsync<HubException>(() => foreign.InvokeAsync("JoinProcessGroup", instance.Id.ToString(), ct));
        if (hub == "monitoring-hub")
        {
            await own.InvokeAsync("JoinTenantGroup", "tenant-a", ct);
            await Assert.ThrowsAsync<HubException>(() => own.InvokeAsync("JoinTenantGroup", "tenant-b", ct));
            await own.InvokeAsync("JoinUserChannel", "reader", ct);
            await Assert.ThrowsAsync<HubException>(() => own.InvokeAsync("JoinUserChannel", "other-user", ct));
            await Assert.ThrowsAsync<HubException>(() => own.InvokeAsync("JoinWorkersGroup", ct));
            await Assert.ThrowsAsync<HubException>(() => own.InvokeAsync("BroadcastSystemMessage", "forged-message", "Info", ct));
        }
    }
}
