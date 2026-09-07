using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class BpmnDeploymentValidatorTests
{
    [Fact]
    public async Task SemanticValidationReportsMalformedXmlWithoutThrowing()
    {
        var result = await new VertexBPMN.Application.SemanticValidationService(new BpmnParser())
            .ValidateBpmnAsync("<definitions", TestContext.Current.CancellationToken);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors!);
    }

    [Fact]
    public async Task SharedClientServerCorpusAndSemanticServiceAgree()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "TestData", "editor-validation-cases.json"), TestContext.Current.CancellationToken);
        using var cases = System.Text.Json.JsonDocument.Parse(json);
        foreach (var item in cases.RootElement.EnumerateArray())
        {
            var xml = item.GetProperty("xml").GetString()!;
            var parser = new BpmnParser();
            var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
            var expected = item.GetProperty("codes").EnumerateArray().Select(c => c.GetString()).Order().ToArray();
            Assert.Equal(expected, BpmnDeploymentValidator.Validate(model).Select(d => d.Code).Order().ToArray());
            var semantic = await new VertexBPMN.Application.SemanticValidationService(parser).ValidateBpmnAsync(xml, TestContext.Current.CancellationToken);
            Assert.Equal(expected.Length == 0, semantic.IsValid);
        }
    }

    private static async Task<IReadOnlyList<ValidationDiagnostic>> Validate(string body)
    {
        var model = await new BpmnParser().ParseAsync($"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="urn:test">
              <process id="p">{body}</process>
            </definitions>
            """, TestContext.Current.CancellationToken);
        return BpmnDeploymentValidator.Validate(model);
    }

    private const string Base = """
        <startEvent id="s"/><endEvent id="e"/><sequenceFlow id="f" sourceRef="s" targetRef="e"/>
        """;

    [Theory]
    [InlineData("userTask")]
    [InlineData("exclusiveGateway")]
    public async Task DetachedCatalogNodeIsRejected(string type)
    {
        var errors = await Validate(Base + $"<{type} id=\"orphan\"/>");
        Assert.Contains(errors, e => e.Code == "DEP-UNREACHABLE-NODE" && e.ElementId == "orphan" && e.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task ConnectedApprovalIsAccepted()
    {
        Assert.Empty(await Validate("""
            <startEvent id="s"/><userTask id="u"/><endEvent id="e"/>
            <sequenceFlow id="a" sourceRef="s" targetRef="u"/><sequenceFlow id="b" sourceRef="u" targetRef="e"/>
            """));
    }

    [Fact]
    public async Task EventSubprocessAndCompensationAreNotOrphans()
    {
        Assert.Empty(await Validate(Base + """
            <userTask id="compensate" isForCompensation="true"/>
            <subProcess id="handler" triggeredByEvent="true"><startEvent id="hs"><errorEventDefinition/></startEvent>
            <endEvent id="he"/><sequenceFlow id="hf" sourceRef="hs" targetRef="he"/></subProcess>
            """));
    }

    [Fact]
    public async Task EmptyIfConditionIsRejected()
    {
        var errors = await Validate("""
            <startEvent id="s"/><exclusiveGateway id="g" default="no"/><endEvent id="e"/>
            <sequenceFlow id="a" sourceRef="s" targetRef="g"/>
            <sequenceFlow id="no" sourceRef="g" targetRef="e"/>
            <sequenceFlow id="yes" sourceRef="g" targetRef="e"><conditionExpression/></sequenceFlow>
            """);
        Assert.Contains(errors, e => e.Code == "DEP-GATEWAY-CONDITION" && e.ElementId == "yes");
    }
}
