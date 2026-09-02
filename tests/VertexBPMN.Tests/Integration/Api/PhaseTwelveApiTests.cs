using System.Net;
using System.Net.Http.Json;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")]
public sealed class PhaseTwelveApiTests
{
    private readonly HttpClient _client;

    public PhaseTwelveApiTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output) =>
        _client = factory.WithSharedFixture(dbFixture).CreateClient(output);

    [Fact]
    public async Task TestRun_DeploysAndStartsBpmnThroughSingleContract()
    {
        var key = $"test-run-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/test-runs", new
        {
            bpmnXml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='start'/><endEvent id='end'/></process></definitions>",
            name = $"{key}.bpmn",
            variables = new Dictionary<string, object> { ["source"] = "test" },
            businessKey = "phase-12"
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(key, payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaseDefinition_DeploysAndCanBeReadBack()
    {
        var key = $"case-{Guid.NewGuid():N}";
        const string cmmn = "<definitions xmlns='https://www.omg.org/spec/CMMN/20151109/MODEL'><case id='case'><casePlanModel id='plan'/></case></definitions>";
        var deploy = await _client.PostAsJsonAsync("/api/case-definitions/deploy", new { key, name = "SDK case", cmmnXml = cmmn }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, deploy.StatusCode);
        var get = await _client.GetAsync($"/api/case-definitions/{key}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }
}
