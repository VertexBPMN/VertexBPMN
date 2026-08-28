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
    public async Task P4_AC_09_DMN_DRD_resolves_required_decision_table_into_literal_expression()
    {
        var key = $"phase4-drd-{Guid.NewGuid():N}";
        var dmn = $$"""
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="risk" name="Risk">
                <decisionTable hitPolicy="UNIQUE">
                  <input id="scoreInput" label="score"><inputExpression typeRef="number"><text>score</text></inputExpression></input>
                  <output id="riskOutput" name="risk" typeRef="string" />
                  <rule><inputEntry><text>&gt;= 700</text></inputEntry><outputEntry><text>"low"</text></outputEntry></rule>
                  <rule><inputEntry><text>&lt; 700</text></inputEntry><outputEntry><text>"high"</text></outputEntry></rule>
                </decisionTable>
              </decision>
              <decision id="{{key}}" name="Approval">
                <informationRequirement><requiredDecision href="#risk" /></informationRequirement>
                <variable name="approved" typeRef="boolean" />
                <literalExpression><text>if risk = "low" then true else false</text></literalExpression>
              </decision>
            </definitions>
            """;
        using var deploy = await _client.PostAsJsonAsync("/api/decision/deploy", new
        {
            decisionKey = key,
            name = "Approval DRD",
            dmnXml = dmn
        }, TestContext.Current.CancellationToken);
        deploy.EnsureSuccessStatusCode();

        using var evaluate = await _client.PostAsJsonAsync("/api/decision/evaluate", new
        {
            decisionKey = key,
            inputs = new Dictionary<string, object> { ["score"] = 720 }
        }, TestContext.Current.CancellationToken);
        evaluate.EnsureSuccessStatusCode();
        using var result = JsonDocument.Parse(await evaluate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(result.RootElement.GetProperty("variables").GetProperty("approved").GetBoolean());
    }

    [Fact]
    public async Task P4_AC_10_DMN_DRD_rejects_dependency_cycles_at_deployment()
    {
        var key = $"phase4-cycle-{Guid.NewGuid():N}";
        var dmn = $$"""
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="{{key}}">
                <informationRequirement><requiredDecision href="#other" /></informationRequirement>
                <literalExpression><text>1</text></literalExpression>
              </decision>
              <decision id="other">
                <informationRequirement><requiredDecision href="#{{key}}" /></informationRequirement>
                <literalExpression><text>2</text></literalExpression>
              </decision>
            </definitions>
            """;
        using var deploy = await _client.PostAsJsonAsync("/api/decision/deploy", new
        {
            decisionKey = key,
            name = "Cyclic DRD",
            dmnXml = dmn
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, deploy.StatusCode);
    }

    [Fact]
    public async Task P4_AC_03_CMMN_case_lifecycle_and_sentries_execute_persistently()
    {
        var key = $"phase4-case-{Guid.NewGuid():N}";
        const string cmmn = """
            <definitions xmlns="https://www.omg.org/spec/CMMN/20151109/MODEL">
              <case id="case" name="Persistent case">
                <casePlanModel id="plan">
                  <planItem id="review" definitionRef="reviewDefinition" />
                  <planItem id="approval" definitionRef="approvalDefinition">
                    <entryCriterion sentryRef="reviewCompleted" />
                  </planItem>
                  <humanTask id="reviewDefinition" name="Review" />
                  <humanTask id="approvalDefinition" name="Approval" />
                  <sentry id="reviewCompleted">
                    <planItemOnPart sourceRef="review"><standardEvent>complete</standardEvent></planItemOnPart>
                  </sentry>
                </casePlanModel>
              </case>
            </definitions>
            """;
        var deploy = await _client.PostAsJsonAsync("/api/case-definitions/deploy", new
        {
            key,
            name = "Persistent case",
            cmmnXml = cmmn
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, deploy.StatusCode);

        var read = await _client.GetAsync($"/api/case-definitions/{key}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var start = await _client.PostAsJsonAsync($"/api/case-definitions/{key}/start", new { },
            TestContext.Current.CancellationToken);
        start.EnsureSuccessStatusCode();
        using var started = JsonDocument.Parse(await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var instanceId = started.RootElement.GetProperty("caseInstanceId").GetGuid();
        Assert.Equal("Active", started.RootElement.GetProperty("state").GetString());
        Assert.Contains("PLAN_ITEM_ACTIVE:review:humantask",
            started.RootElement.GetProperty("trace").EnumerateArray().Select(item => item.GetString()));

        var completeReview = await _client.PostAsJsonAsync(
            $"/api/case-definitions/instances/{instanceId}/plan-items/review/complete",
            new { },
            TestContext.Current.CancellationToken);
        completeReview.EnsureSuccessStatusCode();
        using var reviewed = JsonDocument.Parse(await completeReview.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("PLAN_ITEM_ACTIVE:approval:humantask",
            reviewed.RootElement.GetProperty("trace").EnumerateArray().Select(item => item.GetString()));

        var persisted = await _client.GetAsync(
            $"/api/case-definitions/instances/{instanceId}",
            TestContext.Current.CancellationToken);
        persisted.EnsureSuccessStatusCode();
        using var persistedPayload = JsonDocument.Parse(await persisted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var states = JsonDocument.Parse(persistedPayload.RootElement.GetProperty("planItemStatesJson").GetString()!);
        Assert.Equal("Completed", states.RootElement.GetProperty("review").GetString());
        Assert.Equal("Active", states.RootElement.GetProperty("approval").GetString());

        var completeApproval = await _client.PostAsJsonAsync(
            $"/api/case-definitions/instances/{instanceId}/plan-items/approval/complete",
            new { },
            TestContext.Current.CancellationToken);
        completeApproval.EnsureSuccessStatusCode();
        using var completed = JsonDocument.Parse(await completeApproval.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Completed", completed.RootElement.GetProperty("state").GetString());
        Assert.Contains("CASE_COMPLETED",
            completed.RootElement.GetProperty("trace").EnumerateArray().Select(item => item.GetString()));
    }

    [Theory]
    [InlineData("/api/process-migration/plan/preview")]
    [InlineData("/api/migration/plan")]
    public async Task P4_AC_04_Unqualified_migration_endpoints_return_501(string endpoint)
    {
        var response = await _client.PostAsJsonAsync(endpoint, new { }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task P4_AC_05_Simulation_executes_real_gateway_path_deterministically_and_feeds_analytics()
    {
        const string bpmn = """
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="phase4-simulation" isExecutable="true">
                <startEvent id="start" />
                <exclusiveGateway id="approval" default="toRejected" />
                <serviceTask id="approved" name="Approved path" />
                <serviceTask id="rejected" name="Rejected path" />
                <endEvent id="end" />
                <sequenceFlow id="toGateway" sourceRef="start" targetRef="approval" />
                <sequenceFlow id="toApproved" sourceRef="approval" targetRef="approved">
                  <conditionExpression>approved = true</conditionExpression>
                </sequenceFlow>
                <sequenceFlow id="toRejected" sourceRef="approval" targetRef="rejected" />
                <sequenceFlow id="approvedEnd" sourceRef="approved" targetRef="end" />
                <sequenceFlow id="rejectedEnd" sourceRef="rejected" targetRef="end" />
              </process>
            </definitions>
            """;
        var request = new
        {
            bpmnXml = bpmn,
            processDefinitionId = "phase4-simulation",
            tenantId = "default",
            variables = new Dictionary<string, object> { ["approved"] = true },
            maxSteps = 20
        };

        var firstResponse = await _client.PostAsJsonAsync(
            "/api/simulation",
            request,
            TestContext.Current.CancellationToken);
        firstResponse.EnsureSuccessStatusCode();
        using var firstPayload = JsonDocument.Parse(
            await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var firstSimulation = firstPayload.RootElement.GetProperty("simulation");

        var secondResponse = await _client.PostAsJsonAsync(
            "/api/simulation",
            request,
            TestContext.Current.CancellationToken);
        secondResponse.EnsureSuccessStatusCode();
        using var secondPayload = JsonDocument.Parse(
            await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var secondSimulation = secondPayload.RootElement.GetProperty("simulation");

        Assert.True(firstSimulation.GetProperty("completed").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(firstSimulation.GetProperty("definitionHash").GetString()));
        var firstSteps = firstSimulation.GetProperty("steps").EnumerateArray().ToArray();
        var secondSteps = secondSimulation.GetProperty("steps").EnumerateArray().ToArray();
        var firstIds = firstSteps.Select(step => step.GetProperty("activityId").GetString()).ToArray();
        var secondIds = secondSteps.Select(step => step.GetProperty("activityId").GetString()).ToArray();
        Assert.Equal(new[] { "start", "approval", "approved", "end" }, firstIds);
        Assert.Equal(firstIds, secondIds);
        Assert.Equal(
            firstSteps.Select(step => step.GetProperty("timestamp").GetDateTime()),
            secondSteps.Select(step => step.GetProperty("timestamp").GetDateTime()));

        var analyticsResponse = await _client.PostAsJsonAsync(
            "/api/simulation-analytics/summary",
            firstSimulation.Clone(),
            TestContext.Current.CancellationToken);
        analyticsResponse.EnsureSuccessStatusCode();
        using var analytics = JsonDocument.Parse(
            await analyticsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(4, analytics.RootElement.GetProperty("summary").GetProperty("stepCount").GetInt32());
        Assert.True(analytics.RootElement.GetProperty("summary").GetProperty("completed").GetBoolean());
    }

    [Fact]
    public async Task P4_AC_05b_Simulation_analytics_rejects_untrusted_results()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation-analytics/summary", new
        {
            bpmnXml = "<definitions />",
            definitionHash = "forged",
            processDefinitionId = "phase4",
            completed = true,
            steps = Array.Empty<object>()
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task P4_AC_06_Engine_capabilities_claim_qualified_CMMN_lifecycle_support()
    {
        var response = await _client.GetAsync("/api/engine/capabilities", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.True(payload.RootElement.GetProperty("supportsCmmn").GetBoolean());
    }
}
