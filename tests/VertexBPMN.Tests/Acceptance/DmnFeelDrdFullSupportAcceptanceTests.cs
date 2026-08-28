using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Acceptance;

[Collection("IntegratedApi")]
[Trait("Category", "FullProductSupportAcceptance")]
public sealed class DmnFeelDrdFullSupportAcceptanceTests
{
    private readonly HttpClient _client;

    public DmnFeelDrdFullSupportAcceptanceTests(
        CustomWebApplicationFactory factory,
        SharedSqliteDbFixture database,
        ITestOutputHelper output) =>
        _client = factory.WithSharedFixture(database).CreateClient(output);

    [Fact]
    public async Task FPS_DMN_01_Feel_iteration_context_quantifier_and_temporal_values_execute_through_api()
    {
        var key = $"fps-feel-{Guid.NewGuid():N}";
        var dmn = $$"""
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="{{key}}" name="Advanced FEEL">
                <variable name="analysis" />
                <literalExpression><text>{ doubled: for x in scores return x * 2, allPositive: every x in scores satisfies x &gt; 0, dateOrder: date("2026-08-28") &gt; date("2026-01-01") }</text></literalExpression>
              </decision>
            </definitions>
            """;

        await DeployAsync(key, dmn);
        using var result = await EvaluateAsync(key, new Dictionary<string, object>
        {
            ["scores"] = new[] { 1, 2, 3 }
        });

        var analysis = result.RootElement.GetProperty("variables").GetProperty("analysis");
        Assert.Equal(new[] { 2, 4, 6 }, analysis.GetProperty("doubled").EnumerateArray().Select(x => x.GetInt32()));
        Assert.True(analysis.GetProperty("allPositive").GetBoolean());
        Assert.True(analysis.GetProperty("dateOrder").GetBoolean());
    }

    [Fact]
    public async Task FPS_DMN_02_Multi_level_drd_and_decision_service_return_all_declared_outputs()
    {
        var serviceKey = $"fps-service-{Guid.NewGuid():N}";
        var dmn = $$"""
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <inputData id="applicantData" name="Applicant" />
              <decision id="scoreDecision" name="Score">
                <informationRequirement><requiredInput href="#applicantData" /></informationRequirement>
                <variable name="adjustedScore" />
                <literalExpression><text>score + 10</text></literalExpression>
              </decision>
              <decision id="riskDecision" name="Risk">
                <informationRequirement><requiredDecision href="#scoreDecision" /></informationRequirement>
                <variable name="risk" />
                <literalExpression><text>if adjustedScore &gt;= 80 then "low" else "high"</text></literalExpression>
              </decision>
              <decision id="approvalDecision" name="Approval">
                <informationRequirement><requiredDecision href="#riskDecision" /></informationRequirement>
                <variable name="approved" />
                <literalExpression><text>risk = "low"</text></literalExpression>
              </decision>
              <decisionService id="{{serviceKey}}" name="Underwriting">
                <outputDecision href="#riskDecision" />
                <outputDecision href="#approvalDecision" />
                <encapsulatedDecision href="#scoreDecision" />
                <inputData href="#applicantData" />
              </decisionService>
            </definitions>
            """;

        await DeployAsync(serviceKey, dmn);
        using var result = await EvaluateAsync(serviceKey, new Dictionary<string, object> { ["score"] = 75 });

        var variables = result.RootElement.GetProperty("variables");
        Assert.Equal("low", variables.GetProperty("risk").GetString());
        Assert.True(variables.GetProperty("approved").GetBoolean());
        Assert.False(variables.TryGetProperty("adjustedScore", out _));
    }

    [Theory]
    [InlineData("ANY", null, "\"same\"")]
    [InlineData("COLLECT", "SUM", "30")]
    [InlineData("RULE ORDER", null, "[\"low\",\"high\"]")]
    [InlineData("OUTPUT ORDER", null, "[\"high\",\"low\"]")]
    public async Task FPS_DMN_03_All_multi_hit_policies_execute_with_standard_semantics(
        string hitPolicy,
        string? aggregation,
        string expectedJson)
    {
        var key = $"fps-policy-{Guid.NewGuid():N}";
        var outputValues = hitPolicy == "OUTPUT ORDER"
            ? "<outputValues><text>&quot;high&quot;, &quot;low&quot;</text></outputValues>"
            : string.Empty;
        var firstOutput = hitPolicy == "ANY" ? "same" : "low";
        var secondOutput = hitPolicy == "ANY" ? "same" : "high";
        if (hitPolicy == "COLLECT")
        {
            firstOutput = "10";
            secondOutput = "20";
        }
        var aggregationAttribute = aggregation is null ? string.Empty : $" aggregation=\"{aggregation}\"";
        var dmn = $$"""
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="{{key}}">
                <decisionTable hitPolicy="{{hitPolicy}}"{{aggregationAttribute}}>
                  <input id="amountInput" label="amount"><inputExpression><text>amount</text></inputExpression></input>
                  <output id="resultOutput" name="result">{{outputValues}}</output>
                  <rule><inputEntry><text>&gt; 0</text></inputEntry><outputEntry><text>{{FeelLiteral(firstOutput)}}</text></outputEntry></rule>
                  <rule><inputEntry><text>&gt; 0</text></inputEntry><outputEntry><text>{{FeelLiteral(secondOutput)}}</text></outputEntry></rule>
                </decisionTable>
              </decision>
            </definitions>
            """;

        await DeployAsync(key, dmn);
        using var result = await EvaluateAsync(key, new Dictionary<string, object> { ["amount"] = 1 });
        var actual = result.RootElement.GetProperty("variables").GetProperty("result");
        Assert.Equal(expectedJson, actual.GetRawText());
    }

    [Fact]
    public async Task FPS_DMN_04_Invalid_Feel_is_rejected_during_deployment()
    {
        var key = $"fps-invalid-{Guid.NewGuid():N}";
        var dmn = $$"""
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="{{key}}"><decisionTable hitPolicy="UNIQUE">
                <input id="input"><inputExpression><text>amount</text></inputExpression></input>
                <output id="output" name="result" />
                <rule><inputEntry><text>[1..]</text></inputEntry><outputEntry><text>if true then</text></outputEntry></rule>
              </decisionTable></decision>
            </definitions>
            """;

        using var response = await _client.PostAsJsonAsync("/api/decision/deploy", new
        {
            decisionKey = key,
            name = key,
            dmnXml = dmn
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FPS_DMN_05_Local_ProcessEngine_uses_the_same_drd_and_feel_runtime()
    {
        const string targetDecision = "answerDecision";
        const string dmn = """
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="baseDecision">
                <variable name="baseValue" />
                <literalExpression><text>40</text></literalExpression>
              </decision>
              <decision id="answerDecision">
                <informationRequirement><requiredDecision href="#baseDecision" /></informationRequirement>
                <variable name="answer" />
                <literalExpression><text>baseValue + 2</text></literalExpression>
              </decision>
            </definitions>
            """;
        var parser = new DmnParser(NullLogger<DmnParser>.Instance);
        var dmnEngine = new DmnEngine(NullLogger<DmnEngine>.Instance);
        var processEngine = new ProcessEngine(
            NullLogger<ProcessEngine>.Instance,
            NullServiceTaskRegistry.Instance,
            dmnParser: parser,
            dmnEngine: dmnEngine);
        var variables = new Dictionary<string, object>();
        var model = new BpmnModel(
            "local-dmn-process",
            "Local DMN process",
            Events: [new BpmnEvent("start", "startEvent"), new BpmnEvent("end", "endEvent")],
            Gateways: [],
            Subprocesses: [],
            SequenceFlows:
            [
                new BpmnSequenceFlow("toDecision", "start", "decide"),
                new BpmnSequenceFlow("toEnd", "decide", "end")
            ],
            Tasks:
            [
                new BpmnTask("decide", "businessRuleTask", Attributes: new Dictionary<string, string>
                {
                    ["decisionRef"] = targetDecision
                })
            ],
            ProcessVariables: variables);

        await processEngine.RegisterDmnModelAsync(targetDecision, dmn);
        var trace = await processEngine.ExecuteAsync(model);

        Assert.Contains(trace, entry => entry.Contains($"DecisionEvaluated: {targetDecision} (local)", StringComparison.Ordinal));
        Assert.Equal(42m, variables["answer"]);
    }

    private async Task DeployAsync(string key, string dmn)
    {
        using var response = await _client.PostAsJsonAsync("/api/decision/deploy", new
        {
            decisionKey = key,
            name = key,
            dmnXml = dmn
        }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<JsonDocument> EvaluateAsync(string key, Dictionary<string, object> inputs)
    {
        using var response = await _client.PostAsJsonAsync("/api/decision/evaluate", new
        {
            decisionKey = key,
            inputs
        }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static string FeelLiteral(string value) =>
        decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out _)
            ? value
            : $"\"{value}\"";
}
