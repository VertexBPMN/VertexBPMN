using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Integration.Engine;

/// <summary>
/// Belegt die ECHTE DMN-Decision-Ausführung über den <c>decisionRef</c>-Fallback
/// (der Referenz-Mechanismus der MIWG-Modelle C.8.0/C.8.1, die ein externes
/// „Vacation Approval.dmn" importieren): ein BusinessRuleTask löst den
/// <c>decisionRef</c>-GUID gegen die registrierte, per <c>DmnDecisionGraph</c>
/// evaluierte Entscheidung auf, bindet deren Output als Prozessvariable und lässt
/// das folgende Gateway auf dem ECHTEN Decision-Ergebnis routen.
/// </summary>
public class VacationApprovalDecisionTests
{
    private const string DecisionId = "_9ba1b7b0-c84d-484f-9203-3792edc6dcbd";

    private const string Model =
        @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
                  xmlns:zeebe=""http://camunda.org/schema/zeebe/1.0""
                  targetNamespace=""t"">
  <bpmn:process id=""proc"" isExecutable=""true"">
    <bpmn:startEvent id=""s"" />
    <bpmn:sequenceFlow id=""f1"" sourceRef=""s"" targetRef=""brt"" />
    <bpmn:businessRuleTask id=""brt"" name=""Vacation Approval"" decisionRef=""_9ba1b7b0-c84d-484f-9203-3792edc6dcbd"" />
    <bpmn:sequenceFlow id=""f2"" sourceRef=""brt"" targetRef=""gw"" />
    <bpmn:exclusiveGateway id=""gw"" default=""fRefused"">
      <bpmn:incoming>f2</bpmn:incoming>
      <bpmn:outgoing>fApproved</bpmn:outgoing>
      <bpmn:outgoing>fRefused</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    <bpmn:sequenceFlow id=""fApproved"" sourceRef=""gw"" targetRef=""eApproved"">
      <bpmn:conditionExpression xsi:type=""bpmn:tFormalExpression"">approvalStatus = 'Approved'</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""fRefused"" sourceRef=""gw"" targetRef=""eRefused"" />
    <bpmn:endEvent id=""eApproved"" />
    <bpmn:endEvent id=""eRefused"" />
  </bpmn:process>
</bpmn:definitions>";

    private static BpmnModel Parse(string xml)
    {
        var logger = new Mock<ILogger<BpmnParser>>();
        var parser = new BpmnParser(logger.Object, TracerProvider.Default);
        return parser.ParseAsync(xml, default).GetAwaiter().GetResult();
    }

    private static ProcessEngine BuildEngine(string dmnXml)
    {
        var engine = new ProcessEngine(
            NullLogger<ProcessEngine>.Instance,
            NullServiceTaskRegistry.Instance,
            bpmnParser: null,
            dmnParser: new DmnParser(NullLogger<DmnParser>.Instance),
            dmnEngine: new DmnEngine(NullLogger<DmnEngine>.Instance));
        engine.RegisterDmnModelAsync(DecisionId, dmnXml).GetAwaiter().GetResult();
        return engine;
    }

    private static string LoadDmn() =>
        File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "TestData", "VacationApproval.dmn"));

    [Theory]
    [InlineData(5, "eApproved")]   // numDays <= 10 -> decision output "Approved" -> approved branch
    [InlineData(20, "eRefused")]   // numDays > 10 -> decision output "Refused" -> default branch
    public async Task BusinessRuleTask_Evaluates_Real_Dmn_Via_DecisionRef(int numDays, string expectedEnd)
    {
        var model = Parse(Model);
        var engine = BuildEngine(LoadDmn());

        var trace = engine.Execute(model, new Dictionary<string, object> { ["numDays"] = numDays });

        // Decision must have been evaluated through the local DMN engine (decisionRef -> GUID)
        Assert.Contains(trace, l => l.Contains("DecisionEvaluated:", StringComparison.Ordinal));
        // ... and the gateway routed on the REAL decision output (decision output value, not a fallback).
        Assert.Contains(trace, l => l.Contains("EndEvent: " + expectedEnd, StringComparison.Ordinal));
        Assert.DoesNotContain(trace, l => l.Contains("EndEvent: " + (expectedEnd == "eApproved" ? "eRefused" : "eApproved"), StringComparison.Ordinal));
    }
}
