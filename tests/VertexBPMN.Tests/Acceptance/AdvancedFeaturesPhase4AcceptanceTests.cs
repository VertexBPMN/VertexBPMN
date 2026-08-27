using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Model.Dmn;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Acceptance;

[Collection("IntegratedApi")]
[Trait("Category", "Phase4Acceptance")]
public sealed class AdvancedFeaturesPhase4AcceptanceTests
{
    private readonly HttpClient _client;

    public AdvancedFeaturesPhase4AcceptanceTests(
        CustomWebApplicationFactory factory,
        SharedSqliteDbFixture database,
        ITestOutputHelper output) =>
        _client = factory.WithSharedFixture(database).CreateClient(output);

    [Fact]
    public async Task P4_AC_01_Supported_DMN_subset_deploys_and_evaluates_end_to_end()
    {
        var key = $"phase4-decision-{Guid.NewGuid():N}";
        var dmn = $"""
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="{key}" name="Eligibility">
                <decisionTable hitPolicy="UNIQUE">
                  <input id="age" label="age"><inputExpression typeRef="string" /></input>
                  <output id="result" name="result" typeRef="string" />
                  <rule><inputEntry>18</inputEntry><outputEntry>adult</outputEntry></rule>
                  <rule><inputEntry>17</inputEntry><outputEntry>minor</outputEntry></rule>
                </decisionTable>
              </decision>
            </definitions>
            """;

        var deploy = await _client.PostAsJsonAsync("/api/decision/deploy", new
        {
            decisionKey = key,
            name = "Eligibility",
            dmnXml = dmn
        }, TestContext.Current.CancellationToken);
        deploy.EnsureSuccessStatusCode();

        var evaluation = await _client.PostAsJsonAsync("/api/decision/evaluate", new
        {
            decisionKey = key,
            inputs = new Dictionary<string, object> { ["age"] = "18" }
        }, TestContext.Current.CancellationToken);
        evaluation.EnsureSuccessStatusCode();
        var result = await evaluation.Content.ReadFromJsonAsync<DecisionResult>(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("adult", ((JsonElement)result.Variables["result"]).GetString());
    }

    [Fact]
    public async Task P4_AC_02_Priority_DMN_policy_uses_declared_output_order()
    {
        var key = $"phase4-priority-{Guid.NewGuid():N}";
        var dmn = $"""
            <definitions xmlns="http://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="{key}"><decisionTable hitPolicy="PRIORITY">
                <input id="input"><inputExpression typeRef="number" /></input>
                <output id="output" name="output">
                  <outputValues><text>"high", "medium", "low"</text></outputValues>
                </output>
                <rule><inputEntry>&gt; 10</inputEntry><outputEntry>"medium"</outputEntry></rule>
                <rule><inputEntry>[15..20]</inputEntry><outputEntry>"high"</outputEntry></rule>
              </decisionTable></decision>
            </definitions>
            """;

        var response = await _client.PostAsJsonAsync("/api/decision/deploy", new
        {
            decisionKey = key,
            name = "Priority decision",
            dmnXml = dmn
        }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var evaluation = await _client.PostAsJsonAsync("/api/decision/evaluate", new
        {
            decisionKey = key,
            inputs = new Dictionary<string, object> { ["input"] = 17 }
        }, TestContext.Current.CancellationToken);
        evaluation.EnsureSuccessStatusCode();
        var result = await evaluation.Content.ReadFromJsonAsync<DecisionResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("high", Assert.IsType<JsonElement>(result.Variables["output"]).GetString());
    }

    [Fact]
    public async Task P4_AC_02B_BusinessRuleTask_evaluates_persisted_DMN_and_routes_with_its_output()
    {
        var decisionKey = $"phase4-risk-{Guid.NewGuid():N}";
        var processKey = $"phase4-dmn-process-{Guid.NewGuid():N}";
        var dmn = $$"""
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="{{decisionKey}}" name="Risk decision">
                <decisionTable hitPolicy="UNIQUE">
                  <input id="score"><inputExpression typeRef="number" /></input>
                  <output id="risk" name="risk" typeRef="string" />
                  <rule><inputEntry>&gt;= 700</inputEntry><outputEntry>low</outputEntry></rule>
                  <rule><inputEntry>&lt; 700</inputEntry><outputEntry>high</outputEntry></rule>
                </decisionTable>
              </decision>
            </definitions>
            """;
        var bpmn = $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:zeebe="http://camunda.org/schema/zeebe/1.0">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-decision" sourceRef="start" targetRef="decide" />
                <businessRuleTask id="decide">
                  <extensionElements>
                    <zeebe:calledDecision decisionId="{{decisionKey}}" resultVariable="decisionResult" />
                  </extensionElements>
                </businessRuleTask>
                <sequenceFlow id="to-gateway" sourceRef="decide" targetRef="route" />
                <exclusiveGateway id="route" default="to-reject" />
                <sequenceFlow id="to-approve" sourceRef="route" targetRef="approved">
                  <conditionExpression>${risk == 'low'}</conditionExpression>
                </sequenceFlow>
                <sequenceFlow id="to-reject" sourceRef="route" targetRef="rejected" />
                <endEvent id="approved" />
                <endEvent id="rejected" />
              </process>
            </definitions>
            """;

        using var deployDecision = await _client.PostAsJsonAsync("/api/decision/deploy", new
        {
            decisionKey,
            name = "Risk decision",
            dmnXml = dmn
        }, TestContext.Current.CancellationToken);
        deployDecision.EnsureSuccessStatusCode();

        using var deployProcess = await _client.PostAsJsonAsync("/api/repository", new
        {
            bpmnXml = bpmn,
            name = $"{processKey}.bpmn",
            tenantId = (string?)null
        }, TestContext.Current.CancellationToken);
        deployProcess.EnsureSuccessStatusCode();

        using var start = await _client.PostAsJsonAsync("/api/runtime/start", new
        {
            processDefinitionKey = processKey,
            variables = new Dictionary<string, object> { ["score"] = 720 },
            tenantId = (string?)null
        }, TestContext.Current.CancellationToken);
        start.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(
            await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal((int)ProcessInstanceStatus.Completed, payload.RootElement.GetProperty("status").GetInt32());
        var variables = payload.RootElement.GetProperty("variables");
        Assert.Equal("low", variables.GetProperty("risk").GetString());
        Assert.Equal("low", variables.GetProperty("decisionResult").GetString());
    }

    [Fact]
    public async Task P4_AC_03_CMMN_definition_is_persistent_but_lifecycle_execution_fails_closed()
    {
        var key = $"phase4-case-{Guid.NewGuid():N}";
        const string cmmn = "<definitions xmlns='https://www.omg.org/spec/CMMN/20151109/MODEL'><case id='case'><casePlanModel id='plan'/></case></definitions>";
        var deploy = await _client.PostAsJsonAsync("/api/case-definitions/deploy", new
        {
            key,
            name = "Definition-only case",
            cmmnXml = cmmn
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, deploy.StatusCode);

        var read = await _client.GetAsync($"/api/case-definitions/{key}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var start = await _client.PostAsJsonAsync($"/api/case-definitions/{key}/start", new { },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotImplemented, start.StatusCode);
    }

    [Theory]
    [InlineData("/api/simulation")]
    [InlineData("/api/process-migration/plan/preview")]
    [InlineData("/api/migration/plan")]
    public async Task P4_AC_04_Unqualified_advanced_execution_endpoints_return_501(string endpoint)
    {
        var response = await _client.PostAsJsonAsync(endpoint, new { }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task P4_AC_05_Simulation_analytics_with_valid_input_returns_501()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation-analytics/summary", new
        {
            processDefinitionId = "phase4",
            tenantId = "default",
            completed = true,
            message = "untrusted client result",
            steps = Array.Empty<object>()
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task P4_AC_06_Engine_capabilities_do_not_claim_CMMN_lifecycle_support()
    {
        var response = await _client.GetAsync("/api/engine/capabilities", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.False(payload.RootElement.GetProperty("supportsCmmn").GetBoolean());
    }
}
