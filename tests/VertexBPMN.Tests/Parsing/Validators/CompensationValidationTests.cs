using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Validators;

/// <summary>
/// Belegt die Compensation-Validierung des Unified-Parsers: eine
/// <c>compensateEventDefinition</c> mit gesetztem <c>activityRef</c> muss auf eine
/// existente UND kompensierbare Aktivität zeigen (d.h. eine mit daran befestigtem
/// Compensation-Boundary-Event). Zuvor wurde <c>activityRef</c> nur gespeichert,
/// aber nie validiert (Unified-Gap-Matrix §6).
/// </summary>
public class CompensationValidationTests
{
    private const string Model =
        @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
                  targetNamespace=""t"">
  <bpmn:process id=""proc"" isExecutable=""true"">
    <bpmn:startEvent id=""s"" />
    <bpmn:sequenceFlow id=""f1"" sourceRef=""s"" targetRef=""T1"" />
    <bpmn:task id=""T1"" name=""Demo"" />
    <bpmn:sequenceFlow id=""f2"" sourceRef=""T1"" targetRef=""e"" />
    <bpmn:endEvent id=""e"" />
    <bpmn:intermediateThrowEvent id=""throwMissing"">
      <bpmn:compensateEventDefinition activityRef=""DoesNotExist"" />
    </bpmn:intermediateThrowEvent>
    <bpmn:intermediateThrowEvent id=""throwNotCompensatable"">
      <bpmn:compensateEventDefinition activityRef=""T1"" />
    </bpmn:intermediateThrowEvent>
  </bpmn:process>
</bpmn:definitions>";

    [Fact]
    public async Task Reports_Missing_And_NonCompensatable_ActivityRef()
    {
        var logger = new Mock<ILogger<BpmnParser>>();
        var parser = new BpmnParser(logger.Object, TracerProvider.Default);
        var model = await parser.ParseAsync(Model, TestContext.Current.CancellationToken);

        var diags = model.Diagnostics ?? new string[0];
        Assert.Contains("compensateEventDefinition activityRef DoesNotExist references missing activity (event throwMissing)", diags);
        Assert.Contains(
            "compensateEventDefinition activityRef T1 is not compensatable (no compensation boundary event attached; event throwNotCompensatable)",
            diags);
    }

    [Fact]
    public async Task Accepts_Compensatable_ActivityRef_Without_Diagnostic()
    {
        // T2 has a compensation boundary event attached -> referencing it is valid.
        const string ok = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
                  targetNamespace=""t"">
  <bpmn:process id=""proc"" isExecutable=""true"">
    <bpmn:startEvent id=""s"" />
    <bpmn:sequenceFlow id=""f1"" sourceRef=""s"" targetRef=""T2"" />
    <bpmn:task id=""T2"" name=""Compensatable"" />
    <bpmn:sequenceFlow id=""f2"" sourceRef=""T2"" targetRef=""e"" />
    <bpmn:endEvent id=""e"" />
    <bpmn:intermediateThrowEvent id=""throwOk"">
      <bpmn:compensateEventDefinition activityRef=""T2"" />
    </bpmn:intermediateThrowEvent>
    <bpmn:boundaryEvent id=""compBoundary"" attachedToRef=""T2"" cancelActivity=""false"">
      <bpmn:compensateEventDefinition />
    </bpmn:boundaryEvent>
    <bpmn:sequenceFlow id=""fb"" sourceRef=""compBoundary"" targetRef=""e"" />
  </bpmn:process>
</bpmn:definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();
        var parser = new BpmnParser(logger.Object, TracerProvider.Default);
        var model = await parser.ParseAsync(ok, TestContext.Current.CancellationToken);

        var diags = model.Diagnostics ?? new string[0];
        Assert.DoesNotContain(diags, d => d.Contains("activityRef", System.StringComparison.OrdinalIgnoreCase));
    }
}
