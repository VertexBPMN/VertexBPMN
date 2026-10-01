using System.Net;
using VertexBPMN.ServiceDefaults.Security;

namespace VertexBPMN.Tests.Unit.Security;

public sealed class LocalOidcBackchannelHandlerTests
{
    private const string Public = "http://localhost:58080/realms/vertexbpmn";
    private const string Internal = "http://keycloak:8080/realms/vertexbpmn";

    [Theory]
    [InlineData("http://localhost:58080/realms/vertexbpmn/.well-known/openid-configuration", "http://keycloak:8080/realms/vertexbpmn/.well-known/openid-configuration")]
    [InlineData("http://localhost:58080/realms/vertexbpmn/protocol/openid-connect/token?test=value", "http://keycloak:8080/realms/vertexbpmn/protocol/openid-connect/token?test=value")]
    [InlineData("http://localhost:58080/realms/other/protocol/openid-connect/token", "http://localhost:58080/realms/other/protocol/openid-connect/token")]
    [InlineData("http://attacker.test/realms/vertexbpmn/token", "http://attacker.test/realms/vertexbpmn/token")]
    [InlineData("http://localhost:58080/realms/vertexbpmn-other/token", "http://localhost:58080/realms/vertexbpmn-other/token")]
    public async Task RoutesOnlyTheConfiguredIssuerRealm_WithoutChangingBodyOrPublicMetadata(string address, string expected)
    {
        using var inner = new RecordingHandler();
        using var client = new HttpClient(new LocalOidcBackchannelHandler(Public, Internal, true, inner));
        using var request = new HttpRequestMessage(HttpMethod.Post, address) { Content = new StringContent("test-refresh-body") };
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, inner.Address);
        Assert.Equal("test-refresh-body", inner.Body);
        Assert.Equal(Public, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, Public, Internal)]
    [InlineData(true, "http://issuer.test/realms/vertexbpmn", Internal)]
    [InlineData(true, Public, "http://attacker.test:8080/realms/vertexbpmn")]
    [InlineData(true, Public, "http://keycloak:8080/realms/other")]
    [InlineData(true, Public, "http://keycloak:8080/realms/vertexbpmn?x=1")]
    [InlineData(true, Public, "http://user:password@keycloak:8080/realms/vertexbpmn")]
    public void RejectsProductionOrUnapprovedRouting(bool oidcTest, string authority, string backchannel) =>
        Assert.Throws<InvalidOperationException>(() => LocalOidcBackchannelHandler.Validate(backchannel, authority, oidcTest));

    [Fact]
    public void MissingRouting_LeavesExistingProductionAndLocalProfilesUnchanged() =>
        LocalOidcBackchannelHandler.Validate(null, "https://identity.example.test", false);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Address { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Address = request.RequestUri!.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Public) };
        }
    }
}
