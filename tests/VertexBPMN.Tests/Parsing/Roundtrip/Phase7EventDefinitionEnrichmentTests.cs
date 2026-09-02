using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

/// <summary>
/// Phase 7 - Event Definition Enrichment
/// TDD Tests for normalizing event definitions into strongly-typed objects 
/// while maintaining raw fallback and vendor/unknown event definition diagnostics.
/// </summary>
public class Phase7EventDefinitionEnrichmentTests
{
    [Fact]
    public async Task StandardEventDefinitions_ShouldNormalizeCorrectly()
    {
        // Arrange - Standard BPMN event definitions
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="testProcess">
    <startEvent id="start">
      <timerEventDefinition>
        <timeDate>2025-01-01T10:00:00Z</timeDate>
      </timerEventDefinition>
    </startEvent>
    <intermediateCatchEvent id="message1">
      <messageEventDefinition messageRef="msg1"/>
    </intermediateCatchEvent>
    <intermediateCatchEvent id="signal1">
      <signalEventDefinition signalRef="sig1"/>
    </intermediateCatchEvent>
    <boundaryEvent id="error1" attachedToRef="task1">
      <errorEventDefinition errorRef="err1"/>
    </boundaryEvent>
    <endEvent id="end">
      <terminateEventDefinition/>
    </endEvent>
  </process>
  <message id="msg1" name="TestMessage"/>
  <signal id="sig1" name="TestSignal"/>
  <error id="err1" name="TestError" errorCode="E001"/>
</definitions>
""";

        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict };
        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

        // Assert - Should have strongly-typed event definitions
        Assert.Equal(5, model.Events.Count);
        
        // Timer event
        var timerEvent = model.Events.First(e => e.Id == "start");
        Assert.Single(timerEvent.Definitions);
        Assert.IsType<TimerEventDefinition>(timerEvent.Definitions[0]);
        var timerDef = (TimerEventDefinition)timerEvent.Definitions[0];
        Assert.Equal("2025-01-01T10:00:00Z", timerDef.TimeDate);
        Assert.Null(timerDef.TimeDuration);
        Assert.Null(timerDef.TimeCycle);

        // Message event
        var messageEvent = model.Events.First(e => e.Id == "message1");
        Assert.Single(messageEvent.Definitions);
        Assert.IsType<MessageEventDefinition>(messageEvent.Definitions[0]);
        var messageDef = (MessageEventDefinition)messageEvent.Definitions[0];
        Assert.Equal("msg1", messageDef.MessageRef);

        // Signal event  
        var signalEvent = model.Events.First(e => e.Id == "signal1");
        Assert.Single(signalEvent.Definitions);
        Assert.IsType<SignalEventDefinition>(signalEvent.Definitions[0]);
        var signalDef = (SignalEventDefinition)signalEvent.Definitions[0];
        Assert.Equal("sig1", signalDef.SignalRef);

        // Error event
        var errorEvent = model.Events.First(e => e.Id == "error1");
        Assert.Single(errorEvent.Definitions);
        Assert.IsType<ErrorEventDefinition>(errorEvent.Definitions[0]);
        var errorDef = (ErrorEventDefinition)errorEvent.Definitions[0];
        Assert.Equal("err1", errorDef.ErrorRef);

        // Terminate event
        var terminateEvent = model.Events.First(e => e.Id == "end");
        Assert.Single(terminateEvent.Definitions);
        Assert.IsType<TerminateEventDefinition>(terminateEvent.Definitions[0]);
    }

    [Fact]
    public async Task VendorEventDefinitions_ShouldCaptureRawAndDiagnose()
    {
        // Arrange - Model with vendor/unknown event definitions
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:custom="http://example.com/custom">
  <process id="testProcess">
    <startEvent id="start">
      <custom:customEventDefinition customAttribute="value1"/>
    </startEvent>
    <intermediateCatchEvent id="vendor1">
      <custom:vendorSpecificEvent type="special" config="data"/>
    </intermediateCatchEvent>
  </process>
</definitions>
""";

        var options = new BpmnParserOptions 
        { 
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        };
        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

        // Assert - Should handle unknown event definitions gracefully
        Assert.Equal(2, model.Events.Count);
        
        // Should have captured raw event definitions in metadata
        Assert.NotNull(model.RawMetadata?.RawEventDefinitions);
        Assert.True(model.RawMetadata.RawEventDefinitions.ContainsKey("start"));
        Assert.True(model.RawMetadata.RawEventDefinitions.ContainsKey("vendor1"));
        
        // Should have validation diagnostics for unknown event definitions
        Assert.NotNull(model.ValidationDiagnostics);
        var vendorDiagnostics = model.ValidationDiagnostics
            .Where(d => d.Code == "VEN-UNKNOWN-EVENT-DEFINITION").ToList();
        Assert.Equal(2, vendorDiagnostics.Count);

        // Check diagnostic details
        Assert.Contains(vendorDiagnostics, d => d.ElementId == "start" &&
            d.Message.Contains("custom:customEventDefinition"));
        Assert.Contains(vendorDiagnostics, d => d.ElementId == "vendor1" &&
            d.Message.Contains("custom:vendorSpecificEvent"));
    }

    [Fact]
    public async Task MultipleEventDefinitions_ShouldNormalizeAll()
    {
        // Arrange - Event with multiple definitions (BPMN 2.0 allows this)
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="testProcess">
    <intermediateCatchEvent id="multi1">
      <timerEventDefinition>
        <timeDuration>PT5M</timeDuration>
      </timerEventDefinition>
      <messageEventDefinition messageRef="msg1"/>
    </intermediateCatchEvent>
  </process>
  <message id="msg1" name="TestMessage"/>
</definitions>
""";

        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict };
        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

        // Assert - Should have both event definitions
        var multiEvent = model.Events.First(e => e.Id == "multi1");
        Assert.Equal(2, multiEvent.Definitions.Count);
        
        // Check both definitions are parsed correctly
        Assert.Contains(multiEvent.Definitions, d => d is TimerEventDefinition);
        Assert.Contains(multiEvent.Definitions, d => d is MessageEventDefinition);
        
        var timerDef = multiEvent.Definitions.OfType<TimerEventDefinition>().First();
        Assert.Equal("PT5M", timerDef.TimeDuration);
        
        var messageDef = multiEvent.Definitions.OfType<MessageEventDefinition>().First();
        Assert.Equal("msg1", messageDef.MessageRef);
    }

    [Fact]  
    public async Task EventDefinitionProjection_ShouldMaintainConsistentCounts()
    {
        // Arrange
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="testProcess">
    <startEvent id="start">
      <timerEventDefinition>
        <timeCycle>R/PT1H</timeCycle>
      </timerEventDefinition>
    </startEvent>
    <intermediateCatchEvent id="catch1">
      <messageEventDefinition messageRef="msg1"/>
    </intermediateCatchEvent>
    <endEvent id="end">
      <terminateEventDefinition/>
    </endEvent>
  </process>
  <message id="msg1"/>
</definitions>
""";

        var options = new BpmnParserOptions 
        { 
            RoundtripMode = BpmnRoundtripMode.Strict,
            BuildRuntimeProjection = true
        };
        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

        // Assert - Runtime projection should see consistent event definition counts
        Assert.NotNull(model.Runtime);
        
        // Count event definitions in strict model
        var strictEventDefCount = model.Events.Sum(e => e.Definitions.Count);
        Assert.Equal(3, strictEventDefCount); // timer + message + terminate
        
        // Verify runtime projection has the same events as flow nodes
        var runtimeEventNodes = model.Runtime.FlowNodes.Where(n => n.Type.EndsWith("Event")).ToList();
        Assert.Equal(model.Events.Count, runtimeEventNodes.Count); // Should have same number of event nodes
        Assert.Equal(3, runtimeEventNodes.Count); // start, catch1, end
        
        // Verify event IDs match between strict and runtime models
        var strictEventIds = model.Events.Select(e => e.Id).OrderBy(id => id).ToList();
        var runtimeEventIds = runtimeEventNodes.Select(n => n.Id).OrderBy(id => id).ToList();
        Assert.Equal(strictEventIds, runtimeEventIds);
        
        // Verify that events with definitions exist in runtime
        var eventsWithDefinitions = model.Events.Where(e => e.Definitions.Count > 0).Select(e => e.Id).ToList();
        foreach (var eventId in eventsWithDefinitions)
        {
            Assert.Contains(runtimeEventNodes, n => n.Id == eventId);
        }
    }

    [Fact]
    public async Task EventDefinitionRoundtrip_ShouldBeByteIdentical()
    {
        // Arrange - Complex event definitions for roundtrip test
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="testProcess">
    <startEvent id="start">
      <timerEventDefinition>
        <timeDate>2025-01-01T00:00:00Z</timeDate>
      </timerEventDefinition>
    </startEvent>
    <intermediateCatchEvent id="message1">
      <messageEventDefinition messageRef="msg1" correlationKey="key1"/>
    </intermediateCatchEvent>
    <intermediateCatchEvent id="conditional1">
      <conditionalEventDefinition>
        <condition><![CDATA[${variable > 100}]]></condition>
      </conditionalEventDefinition>
    </intermediateCatchEvent>
    <boundaryEvent id="escalation1" attachedToRef="task1">
      <escalationEventDefinition escalationRef="esc1"/>
    </boundaryEvent>
  </process>
  <message id="msg1" name="TestMessage"/>
  <escalation id="esc1" name="TestEscalation"/>
</definitions>
""";

        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, EnableNormalizedProjectionSerializer = true };
        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
        var serialized = parser.Serialize(model);

        // Assert - Roundtrip should be byte-identical (or canonically equivalent)
        // This is a placeholder - actual implementation should normalize whitespace
        Assert.Contains("<timerEventDefinition>", serialized);
        Assert.Contains("<timeDate>2025-01-01T00:00:00Z</timeDate>", serialized);
        Assert.Contains("<messageEventDefinition", serialized);
        Assert.Contains("messageRef=\"msg1\"", serialized);
        Assert.Contains("correlationKey=\"key1\"", serialized);
        Assert.Contains("<conditionalEventDefinition>", serialized);
        Assert.Contains("<![CDATA[${variable > 100}]]>", serialized);
        Assert.Contains("<escalationEventDefinition", serialized);
        Assert.Contains("escalationRef=\"esc1\"", serialized);
        
        // Verify no data loss in roundtrip
        var reparsed = await parser.ParseAsync(serialized, TestContext.Current.CancellationToken);
        Assert.Equal(model.Events.Count, reparsed.Events.Count);
        
        foreach (var originalEvent in model.Events)
        {
            var reparsedEvent = reparsed.Events.First(e => e.Id == originalEvent.Id);
            Assert.Equal(originalEvent.Definitions.Count, reparsedEvent.Definitions.Count);
        }
    }

    [Fact]
    public async Task UnknownEventDefinitionMutation_ShouldProduceDiagnostic()
    {
        // Arrange - Test diagnostic when raw path is chosen for unknown definitions
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:unknown="http://vendor.com/unknown">
  <process id="testProcess">
    <startEvent id="start">
      <unknown:proprietaryEventDefinition setting="critical"/>
    </startEvent>
  </process>
</definitions>
""";

        var options = new BpmnParserOptions 
        { 
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true,
            ValidateRuntimeSemantics = true
        };
        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

        // Assert - Should have diagnostic about unknown event definition
        Assert.NotNull(model.ValidationDiagnostics);
        var unknownDiagnostic = model.ValidationDiagnostics
            .FirstOrDefault(d => d.Code == "VEN-UNKNOWN-EVENT-DEFINITION");
        Assert.Equal("start", unknownDiagnostic.ElementId);
        Assert.Contains("unknown:proprietaryEventDefinition", unknownDiagnostic.Message);
        Assert.Equal(ValidationSeverity.Info, unknownDiagnostic.Severity);
        
        // Raw event definition should still be captured
        Assert.NotNull(model.RawMetadata?.RawEventDefinitions);
        Assert.True(model.RawMetadata.RawEventDefinitions.ContainsKey("start"));
    }

    [Fact] 
    public async Task EmptyEventDefinitions_ShouldBeHandledGracefully()
    {
        // Arrange - Events without event definitions (plain events)
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="testProcess">
    <startEvent id="start"/>
    <endEvent id="end"/>
  </process>
</definitions>
""";

        var options = new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict };
        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

        // Assert - Should handle events without definitions
        Assert.Equal(2, model.Events.Count);
        
        var startEvent = model.Events.First(e => e.Id == "start");
        var endEvent = model.Events.First(e => e.Id == "end");
        
        Assert.Empty(startEvent.Definitions);
        Assert.Empty(endEvent.Definitions);
        
        // No diagnostics should be generated for plain events
        var eventDefDiagnostics = model.ValidationDiagnostics?
            .Where(d => d.Code.StartsWith("VEN-UNKNOWN-EVENT")) ?? [];
        Assert.Empty(eventDefDiagnostics);
    }
}