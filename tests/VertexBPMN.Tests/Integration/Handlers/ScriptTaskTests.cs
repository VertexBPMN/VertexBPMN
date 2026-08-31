using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using Shouldly;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Infrastructure.Scripting;

namespace VertexBPMN.Tests.Integration.Handlers;

public class ScriptTaskTests
{
    [Fact]
    public async Task CSharp_ScriptTask_AddsNumbers_AndStoresResult()
    {
        // Arrange: Minimalprozess mit ScriptTask (C#)
        var xml = @"
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' id='Defs'>
  <process id='scriptDemo' isExecutable='true'>
    <startEvent id='start' />
    <sequenceFlow id='f1' sourceRef='start' targetRef='doMath' />
    <scriptTask id='doMath' name='Addiere' scriptFormat='C#' resultVariable='sum'>
      <script><![CDATA[
        var a = (int)variables[""a""];
        var b = (int)variables[""b""];
        return a + b;
      ]]></script>
    </scriptTask>
    <sequenceFlow id='f2' sourceRef='doMath' targetRef='end' />
    <endEvent id='end' />
  </process>
</definitions>";

        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
        var model =  await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);

        // Nimm den ScriptTask aus dem Modell
        var task = model.Tasks.Single(t => t.Type == "scriptTask");

        // Variablen (so wie deine Engine sie hält)
        var variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = 2,
            ["b"] = 40
        };

        // Act: Direkt den Runner testen (unabhängig vom Engine-Loop)
        var handled = await ScriptTaskExecution.TryHandleScriptTaskAsync(task, variables, TestContext.Current.CancellationToken);

        // Assert
        handled.ShouldBe(true);
        variables.ShouldContainKey("sum");
        Convert.ToInt32(variables["sum"]).ShouldBe(42);
    }

    [Fact]
    public async Task JavaScript_ScriptTask_Works_With_Jint()
    {
        var xml = @"
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' id='Defs'>
  <process id='scriptDemo' isExecutable='true'>
    <startEvent id='start' />
    <sequenceFlow id='f1' sourceRef='start' targetRef='doJs' />
    <scriptTask id='doJs' name='JS' scriptFormat='JavaScript' resultVariable='out'>
      <script><![CDATA[
        const x = variables[""x""];
        const y = variables[""y""];
        x + y;
      ]]></script>
    </scriptTask>
    <sequenceFlow id='f2' sourceRef='doJs' targetRef='end' />
    <endEvent id='end' />
  </process>
</definitions>";

        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
        var model =  await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
        var task = model.Tasks.Single(t => t.Type == "scriptTask");

        var variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = 10,
            ["y"] = 5
        };

        var handled = await ScriptTaskExecution.TryHandleScriptTaskAsync(task, variables, TestContext.Current.CancellationToken);

        handled.ShouldBe(true);
        variables.ShouldContainKey("out");
        Convert.ToInt32(variables["out"]).ShouldBe(15);
    }

    [Fact]
    public async Task ScriptTask_Should_Update_ProcessVariable()
    {
        // Arrange: Minimaler BPMN Task
        var scriptTask = new BpmnTask
        (
            Id: "task1",
            Type: "scriptTask",
            Attributes: new Dictionary<string, string>
                {
                    { "scriptFormat", "C#" },
                    { "script", "variables[\"sum\"] = (int)variables[\"a\"] + (int)variables[\"b\"]; return variables[\"sum\"]; " },
                    { "resultVariable", "sum" }
                }
        );

        var model = new BpmnModel(
           ProcessId: "process1",
           Name: "TestProcess",
            new List<BpmnEvent>(),
            new List<BpmnGateway>(),
            new List<BpmnSubprocess>(),
            new List<BpmnSequenceFlow>(),
            new List<BpmnTask> { scriptTask },
            ProcessVariables: new Dictionary<string, object>
            {
                    { "a", 2 },
                    { "b", 3 }
            }
        );

        // Act
        Assert.NotNull(model.ProcessVariables);
        var variables = model.ProcessVariables!;
        var handled = await ScriptTaskExecution.TryHandleScriptTaskAsync(scriptTask, variables, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.True(variables.ContainsKey("sum"));
        Assert.Equal(5, variables["sum"]);
    }

    [Fact]
    public async Task ScriptTask_Should_Support_JavaScript()
    {
        // Arrange
        var scriptTask = new BpmnTask(
            Id:"task2",
            Type:"scriptTask",
            Attributes :new Dictionary<string, string>
                {
                    { "scriptFormat", "JavaScript" },
                    { "script", "variables.sum = variables.a + variables.b; variables.sum;" },
                    { "resultVariable", "sum" }
                }
        );

        var model = new BpmnModel(
            "process2",
             "TestProcessJS",
            new List<BpmnEvent>(),
            new List<BpmnGateway>(),
            new List<BpmnSubprocess>(),
            new List<BpmnSequenceFlow>(),
            new List<BpmnTask> { scriptTask },
            ProcessVariables:new Dictionary<string, object>
            {
                    { "a", 7 },
                    { "b", 8 }
            }
        );

        // Act
        Assert.NotNull(model.ProcessVariables);
        var variables = model.ProcessVariables!;
        var handled = await ScriptTaskExecution.TryHandleScriptTaskAsync(scriptTask, variables, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.True(variables.ContainsKey("sum"));
        Assert.Equal(15, Convert.ToInt32(variables["sum"]));
    }
}

