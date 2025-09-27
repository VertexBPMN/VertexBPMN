using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Parsing;
using VertexBPMN.Parsing.Serialization;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

/// <summary>
/// Phase 8 - Namespace & Serialization Harmonization
/// TDD tests for deterministic normalization serializer, canonical sorting, 
/// and hash-of-structural-model for cache invalidation.
/// </summary>
public class Phase8NamespaceSerializationHarmonizationTests
{
    [Fact]
    public async Task NormalizedProjectionSerializer_ShouldProduceDeterministicOutput()
    {
        // Arrange - Same logical model with different namespace prefixes/ordering
        const string xml1 = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
             xmlns:zeebe="http://zeebe.io/schema/zeebe/1.0">
  <process id="testProcess">
    <startEvent id="start"/>
    <sequenceFlow id="flow1" sourceRef="start" targetRef="task1"/>
    <userTask id="task1" camunda:assignee="user1"/>
    <sequenceFlow id="flow2" sourceRef="task1" targetRef="end"/>
    <endEvent id="end"/>
  </process>
</definitions>
""";
        
        const string xml2 = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:zeebe="http://zeebe.io/schema/zeebe/1.0"
             xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <process id="testProcess">
    <startEvent id="start"/>
    <sequenceFlow id="flow1" sourceRef="start" targetRef="task1"/>
    <userTask id="task1" camunda:assignee="user1"/>
    <sequenceFlow id="flow2" sourceRef="task1" targetRef="end"/>
    <endEvent id="end"/>
  </process>
</definitions>
""";

        var options = new BpmnParserOptions 
        { 
            RoundtripMode = BpmnRoundtripMode.Normalized,
            EnableNormalizedProjectionSerializer = true,
            EnableCanonicalSort = true
        };
        var parser = new BpmnParser(options);

        // Act
        var model1 = await parser.ParseAsync(xml1);
        var model2 = await parser.ParseAsync(xml2);

        var normalizedSerializer = new NormalizedProjectionSerializer(options);
        var serialized1 = normalizedSerializer.Serialize(model1);
        var serialized2 = normalizedSerializer.Serialize(model2);

        // Assert - Should produce identical normalized output
        Assert.Equal(serialized1, serialized2);
        Assert.Contains("xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"", serialized1);
        Assert.Contains("xmlns:camunda=\"http://camunda.org/schema/1.0/bpmn\"", serialized1);
        
        // Should be canonical ordering of elements
        var doc = XDocument.Parse(serialized1);
        var processElements = doc.Descendants().Where(e => e.Name.LocalName == "startEvent" || 
                                                          e.Name.LocalName == "userTask" ||
                                                          e.Name.LocalName == "endEvent" ||
                                                          e.Name.LocalName == "sequenceFlow").ToList();
        
        // Elements should be in canonical order: events first, then tasks, then gateways, then flows
        var startEventIndex = processElements.FindIndex(e => e.Name.LocalName == "startEvent");
        var userTaskIndex = processElements.FindIndex(e => e.Name.LocalName == "userTask");
        var endEventIndex = processElements.FindIndex(e => e.Name.LocalName == "endEvent");
        var flowIndices = processElements.Where((e, i) => e.Name.LocalName == "sequenceFlow").Select((e, i) => processElements.IndexOf(e)).ToList();

        Assert.True(startEventIndex < userTaskIndex, "Start event should come before user task in canonical order");
        Assert.True(userTaskIndex < endEventIndex, "User task should come before end event in canonical order");
        foreach (var flowIndex in flowIndices)
        {
            Assert.True(flowIndex > endEventIndex, "Sequence flows should come after all other elements in canonical order");
        }
    }

    [Fact] 
    public async Task StructuralModelHash_ShouldBeStableForLogicallyEquivalentModels()
    {
        // Arrange - Same logical model with different superficial differences
        const string xml1 = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="Process_1" name="Test Process">
    <startEvent id="StartEvent_1"/>
    <userTask id="Task_1" name="User Task"/>
    <endEvent id="EndEvent_1"/>
    <sequenceFlow id="Flow_1" sourceRef="StartEvent_1" targetRef="Task_1"/>
    <sequenceFlow id="Flow_2" sourceRef="Task_1" targetRef="EndEvent_1"/>
  </process>
</definitions>
""";

        const string xml2 = """
<definitions   xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process   id="Process_1"   name="Test Process">
    <sequenceFlow id="Flow_1" sourceRef="StartEvent_1" targetRef="Task_1"  />
    <startEvent id="StartEvent_1"  />
    <endEvent  id="EndEvent_1" />
    <userTask id="Task_1"  name="User Task" />
    <sequenceFlow id="Flow_2"  sourceRef="Task_1"  targetRef="EndEvent_1"/>
  </process>
</definitions>
""";

        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Normalized };
        var parser = new BpmnParser(options);

        // Act
        var model1 = await parser.ParseAsync(xml1);
        var model2 = await parser.ParseAsync(xml2);

        var hash1 = parser.ComputeStructuralModelHash(model1);
        var hash2 = parser.ComputeStructuralModelHash(model2);

        // Assert - Should produce same hash for logically equivalent models
        Assert.Equal(hash1, hash2);
        Assert.NotNull(hash1);
        Assert.NotEmpty(hash1);
        
        // Hash should be consistent across multiple calls
        var hash1Again = parser.ComputeStructuralModelHash(model1);
        Assert.Equal(hash1, hash1Again);
    }

    [Fact]
    public async Task StructuralModelHash_ShouldBeDifferentForDifferentModels()
    {
        // Arrange - Different logical models
        const string xml1 = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="Process_1">
    <startEvent id="start"/>
    <userTask id="task1"/>
    <endEvent id="end"/>
    <sequenceFlow id="flow1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="flow2" sourceRef="task1" targetRef="end"/>
  </process>
</definitions>
""";

        const string xml2 = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="Process_1">
    <startEvent id="start"/>
    <serviceTask id="task1"/>
    <endEvent id="end"/>
    <sequenceFlow id="flow1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="flow2" sourceRef="task1" targetRef="end"/>
  </process>
</definitions>
""";

        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Normalized };
        var parser = new BpmnParser(options);

        // Act
        var model1 = await parser.ParseAsync(xml1);
        var model2 = await parser.ParseAsync(xml2);

        var hash1 = parser.ComputeStructuralModelHash(model1);
        var hash2 = parser.ComputeStructuralModelHash(model2);

        // Assert - Should produce different hashes for different models
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task CanonicalSort_ShouldOrderElementsConsistently()
    {
        // Arrange
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="testProcess">
    <sequenceFlow id="flow3" sourceRef="gateway1" targetRef="end"/>
    <endEvent id="end"/>
    <userTask id="task1"/>
    <startEvent id="start"/>
    <exclusiveGateway id="gateway1"/>
    <sequenceFlow id="flow1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="flow2" sourceRef="task1" targetRef="gateway1"/>
  </process>
</definitions>
""";

        var options = new BpmnParserOptions 
        { 
            RoundtripMode = BpmnRoundtripMode.Normalized,
            EnableCanonicalSort = true 
        };
        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(xml);
        var normalizedSerializer = new NormalizedProjectionSerializer(options);
        var serialized = normalizedSerializer.Serialize(model);

        // Assert - Should follow canonical element ordering
        var doc = XDocument.Parse(serialized);
        var processElement = doc.Descendants().First(e => e.Name.LocalName == "process");
        var childElements = processElement.Elements().ToList();

        var elementTypes = childElements.Select(e => e.Name.LocalName).ToList();
        
        // Expected canonical order: events, activities (tasks), gateways, flows
        var startEventIndex = elementTypes.IndexOf("startEvent");
        var endEventIndex = elementTypes.IndexOf("endEvent");
        var taskIndex = elementTypes.IndexOf("userTask");
        var gatewayIndex = elementTypes.IndexOf("exclusiveGateway");
        var firstFlowIndex = elementTypes.IndexOf("sequenceFlow");
        
        Assert.True(startEventIndex >= 0 && startEventIndex < taskIndex, "Start event should come before task");
        Assert.True(endEventIndex >= 0 && endEventIndex < taskIndex, "End event should come before task");
        Assert.True(taskIndex >= 0 && taskIndex < gatewayIndex, "Task should come before gateway");
        Assert.True(gatewayIndex >= 0 && gatewayIndex < firstFlowIndex, "Gateway should come before sequence flows");
    }

    [Fact]
    public async Task NormalizedSerializer_ShouldPreserveSemanticContent()
    {
        // Arrange - Complex model with various elements
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="testProcess" name="Test Process">
    <startEvent id="start" name="Start"/>
    <userTask id="task1" name="User Task">
      <documentation>Task documentation</documentation>
    </userTask>
    <exclusiveGateway id="gateway1" name="Decision"/>
    <userTask id="task2" name="Branch A"/>
    <userTask id="task3" name="Branch B"/>
    <exclusiveGateway id="gateway2" name="Merge"/>
    <endEvent id="end" name="End"/>
    <sequenceFlow id="flow1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="flow2" sourceRef="task1" targetRef="gateway1"/>
    <sequenceFlow id="flow3" sourceRef="gateway1" targetRef="task2" name="Option A">
      <conditionExpression><![CDATA[${option == 'A'}]]></conditionExpression>
    </sequenceFlow>
    <sequenceFlow id="flow4" sourceRef="gateway1" targetRef="task3" name="Option B">
      <conditionExpression><![CDATA[${option == 'B'}]]></conditionExpression>
    </sequenceFlow>
    <sequenceFlow id="flow5" sourceRef="task2" targetRef="gateway2"/>
    <sequenceFlow id="flow6" sourceRef="task3" targetRef="gateway2"/>
    <sequenceFlow id="flow7" sourceRef="gateway2" targetRef="end"/>
  </process>
</definitions>
""";

        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Normalized };
        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(xml);
        var normalizedSerializer = new NormalizedProjectionSerializer(options);
        var serialized = normalizedSerializer.Serialize(model);

        // Reparse to verify semantic preservation
        var reparsed = await parser.ParseAsync(serialized);

        // Assert - All semantic content should be preserved
        Assert.Equal(model.ProcessId, reparsed.ProcessId);
        Assert.Equal(model.Events.Count, reparsed.Events.Count);
        Assert.Equal(model.Tasks.Count, reparsed.Tasks.Count);
        Assert.Equal(model.Gateways.Count, reparsed.Gateways.Count);
        Assert.Equal(model.SequenceFlows.Count, reparsed.SequenceFlows.Count);

        // Verify specific elements
        var originalTask1 = model.Tasks.First(t => t.Id == "task1");
        var reparsedTask1 = reparsed.Tasks.First(t => t.Id == "task1");
        Assert.Equal(originalTask1.Name, reparsedTask1.Name);

        // Verify condition expressions are preserved
        var originalFlow3 = model.SequenceFlows.First(f => f.Id == "flow3");
        var reparsedFlow3 = reparsed.SequenceFlows.First(f => f.Id == "flow3");
        Assert.Equal(originalFlow3.ConditionExpression, reparsedFlow3.ConditionExpression);
        Assert.Equal("${option == 'A'}", reparsedFlow3.ConditionExpression);
    }

    [Fact]
    public async Task StrictSerializerNoRegressions_ShouldMaintainExistingBehavior()
    {
        // Arrange - Ensure strict serializer is not affected by Phase 8 changes
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <process id="testProcess">
    <startEvent id="start"/>
    <userTask id="task1" camunda:assignee="user1">
      <extensionElements>
        <camunda:properties>
          <camunda:property name="priority" value="high"/>
        </camunda:properties>
      </extensionElements>
    </userTask>
    <endEvent id="end"/>
    <sequenceFlow id="flow1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="flow2" sourceRef="task1" targetRef="end">
      <conditionExpression><![CDATA[${approved}]]></conditionExpression>
    </sequenceFlow>
  </process>
</definitions>
""";

        var strictOptions = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict };
        var parser = new BpmnParser(strictOptions);

        // Act
        var model = await parser.ParseAsync(xml);
        var strictSerialized = parser.Serialize(model); // Uses existing BpmnSerializer

        // Assert - Should be able to roundtrip with strict serializer unchanged
        var reparsed = await parser.ParseAsync(strictSerialized);
        
        Assert.Equal(model.ProcessId, reparsed.ProcessId);
        Assert.Equal(model.Tasks.Count, reparsed.Tasks.Count);
        
        // Verify raw metadata is preserved in strict mode
        Assert.NotNull(model.RawMetadata);
        Assert.NotNull(reparsed.RawMetadata);
        
        // Verify extensions are preserved
        var originalTask = model.Tasks.First(t => t.Id == "task1");
        var reparsedTask = reparsed.Tasks.First(t => t.Id == "task1");
        Assert.NotNull(originalTask.Extensions);
        Assert.NotNull(reparsedTask.Extensions);
        Assert.True(originalTask.Extensions.ContainsKey("camunda:assignee") ||
                   originalTask.Extensions.ContainsKey("camunda:properties:__present"));
    }

    [Fact]
    public async Task StructuralEquality_SameLogicalModel_ShouldHaveSameHash()
    {
        // Arrange - Test the structural equality aspect specifically
        const string baseXml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="proc1">
    <startEvent id="start"/>
    <userTask id="task1" name="Task 1"/>
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="f2" sourceRef="task1" targetRef="end"/>
  </process>
</definitions>
""";

        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Normalized };
        var parser = new BpmnParser(options);

        // Act - Parse the same logical model multiple times
        var model1 = await parser.ParseAsync(baseXml);
        var model2 = await parser.ParseAsync(baseXml);
        
        var hash1 = parser.ComputeStructuralModelHash(model1);
        var hash2 = parser.ComputeStructuralModelHash(model2);

        // Assert - Same logical content should produce same hash
        Assert.Equal(hash1, hash2);
        
        // Hash should be deterministic and non-empty
        Assert.NotNull(hash1);
        Assert.NotEmpty(hash1);
        Assert.Matches(@"^[A-F0-9]+$", hash1); // Should be hex string
    }
}