using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Integration.Bpmn;

/// <summary>
/// Belegt, dass der Datenobjekt-Zugriff <c>bpmn:getDataObject('x')</c> auch im FEEL-Pfad
/// (Bedingung mit Vergleichsoperator, z.B. <c>getDataObject('status') = 'ok'</c>) gegen die
/// Prozessvariable aufgelöst wird — nicht nur im Jint-/Fallback-Pfad. Vor dem Fix warf die
/// FEEL-Runtime auf den Doppelpunkt im Funktionsnamen ("Unrecognized token").
/// </summary>
public class GetDataObjectFeelTests
{
    private const string Model =
        @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
                  xmlns:zeebe=""http://camunda.org/schema/zeebe/1.0""
                  targetNamespace=""t"">
  <bpmn:process id=""proc"" isExecutable=""true"">
    <bpmn:startEvent id=""s"" />
    <bpmn:sequenceFlow id=""f1"" sourceRef=""s"" targetRef=""gw"" />
    <bpmn:exclusiveGateway id=""gw"" default=""fNo"">
      <bpmn:incoming>f1</bpmn:incoming>
      <bpmn:outgoing>fYes</bpmn:outgoing>
      <bpmn:outgoing>fNo</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    <bpmn:sequenceFlow id=""fYes"" sourceRef=""gw"" targetRef=""eYes"">
      <bpmn:conditionExpression xsi:type=""bpmn:tFormalExpression"">bpmn:getDataObject('status') = 'ok'</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""fNo"" sourceRef=""gw"" targetRef=""eNo"" />
    <bpmn:endEvent id=""eYes"" />
    <bpmn:endEvent id=""eNo"" />
  </bpmn:process>
</bpmn:definitions>";

    [Theory]
    [InlineData("ok", "eYes")]    // FEEL getDataObject resolves -> 'ok' branch
    [InlineData("fail", "eNo")]   // condition false -> default branch
    public async Task Executes_GetDataObject_Condition_Through_Engine(string status, string expectedEnd)
    {
        var logger = new Mock<ILogger<BpmnParser>>();
        var parser = new BpmnParser(logger.Object, TracerProvider.Default);
        var model = await parser.ParseAsync(Model, TestContext.Current.CancellationToken);

        var vars = new Dictionary<string, object> { ["status"] = status };
        var engine = new ProcessEngine();
        var trace = engine.Execute(model, vars);

        Assert.NotNull(trace);
        Assert.NotEmpty(trace!);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in trace!)
        {
            foreach (var id in new[] { "eYes", "eNo" })
                if (line.Contains(id, StringComparison.Ordinal))
                    counts[id] = counts.GetValueOrDefault(id) + 1;
        }
        var executed = counts.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        Assert.Equal(expectedEnd, Assert.Single(executed));
    }
}
