using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validation;

public class Phase3AdvancedValidationThrowEscalationTests
{
    private const string DuplicateIdXml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="p1">
    <bpmn:startEvent id="s1"/>
    <bpmn:userTask id="dup"/>
    <bpmn:serviceTask id="dup"/>
    <bpmn:endEvent id="e1"/>
    <bpmn:sequenceFlow id="f1" sourceRef="s1" targetRef="dup"/>
    <bpmn:sequenceFlow id="f2" sourceRef="dup" targetRef="e1"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public async Task EscalationDisabled_DoesNotThrow()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true,
            ThrowOnFatalValidation = false
        }).ParseAsync(DuplicateIdXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.Contains(model.ValidationDiagnostics!, d => d.Code == "STR-DUP-ID");
    }

    [Fact]
    public async Task EscalationEnabled_ThrowsOnError()
    {
        var ex = await Assert.ThrowsAsync<BpmnValidationException>(async () =>
        {
            await new BpmnParser(new BpmnParserOptions
            {
                RoundtripMode = BpmnRoundtripMode.Strict,
                EnableAdvancedValidation = true,
                ThrowOnFatalValidation = true,
                MinimumThrowSeverity = ValidationSeverity.Error
            }).ParseAsync(DuplicateIdXml, TestContext.Current.CancellationToken);
        });

        Assert.Contains("STR-DUP-ID", ex.Diagnostics.Select(d => d.Code));
    }

    [Fact]
    public async Task EscalationThresholdHigher_NoThrow()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true,
            ThrowOnFatalValidation = true,
            MinimumThrowSeverity = ValidationSeverity.Fatal // no Fatal present
        }).ParseAsync(DuplicateIdXml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.ValidationDiagnostics);
        Assert.Contains(model.ValidationDiagnostics!, d => d.Code == "STR-DUP-ID");
    }

    [Fact]
    public async Task MissingProcess_ThrowsWhenEnabled()
    {
        const string noProcess = "<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\" />";
        var ex = await Assert.ThrowsAsync<BpmnValidationException>(async () =>
        {
            await new BpmnParser(new BpmnParserOptions
            {
                RoundtripMode = BpmnRoundtripMode.Strict,
                EnableAdvancedValidation = true,
                ThrowOnFatalValidation = true,
                MinimumThrowSeverity = ValidationSeverity.Error
            }).ParseAsync(noProcess, TestContext.Current.CancellationToken);
        });
        Assert.Contains(ex.Diagnostics, d => d.Code == "STR-MISSING-PROCESS");
    }
}
