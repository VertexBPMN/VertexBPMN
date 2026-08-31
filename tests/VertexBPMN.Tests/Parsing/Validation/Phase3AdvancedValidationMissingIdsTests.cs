using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class Phase3AdvancedValidationMissingIdsTests
{
    private const string NoProcessXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
</bpmn:definitions>
""";

    private const string MissingIdsXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent/>
    <bpmn:serviceTask name="TaskWithoutId"/>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="__missingStart" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public async Task MissingProcess_Disabled_NoStructuredDiagnostic()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(NoProcessXml, TestContext.Current.CancellationToken);

        Assert.Null(model.ValidationDiagnostics);
        Assert.Contains(model.Diagnostics, d => d.StartsWith("No <process> element"));
    }

    [Fact]
    public async Task MissingProcess_Enabled_ReportsStructured()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(NoProcessXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.Contains(model.ValidationDiagnostics!, d =>
            d.Code == "STR-MISSING-PROCESS" &&
            d.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task MissingIds_Enabled_ReportsEachMissingId()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        }).ParseAsync(MissingIdsXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);

        // Expect at least two STR-MISSING-ID (startEvent + serviceTask)
        var count = model.ValidationDiagnostics!.Count(d => d.Code == "STR-MISSING-ID");
        Assert.True(count >= 2, $"Expected >=2 STR-MISSING-ID diagnostics, got {count}");

        // Ensure they reference types in message text
        Assert.Contains(model.ValidationDiagnostics!, d => d.Code == "STR-MISSING-ID" && d.Message.Contains("startEvent"));
        Assert.Contains(model.ValidationDiagnostics!, d => d.Code == "STR-MISSING-ID" && d.Message.Contains("serviceTask"));
    }

    [Fact]
    public async Task MissingIds_Disabled_NoStructuredDiagnostics()
    {
        var model = await new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = false
        }).ParseAsync(MissingIdsXml, TestContext.Current.CancellationToken);

        Assert.Null(model.ValidationDiagnostics);
    }
}