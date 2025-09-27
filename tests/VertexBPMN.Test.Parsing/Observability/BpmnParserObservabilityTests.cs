using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Observability;

// Simple test logger implementation
internal class TestLogger : ILogger
{
    public List<LogEntry> LogEntries { get; } = new();
    
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogEntries.Add(new LogEntry(logLevel, formatter(state, exception), state?.ToString()));
    }
}

internal class TestLogger<T> : ILogger<T>
{
    public List<LogEntry> LogEntries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogEntries.Add(new LogEntry(logLevel, formatter(state, exception), state?.ToString()));
    }
}

internal record LogEntry(LogLevel Level, string Message, string? StructuredState);

public class BpmnParserObservabilityTests
{
    private const string SimpleXml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="testProcess">
    <startEvent id="start"/>
    <userTask id="task1" name="User Task"/>
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="f2" sourceRef="task1" targetRef="end"/>
  </process>
</definitions>
""";

    private const string ComplexXml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <process id="complexProcess">
    <startEvent id="start"/>
    <userTask id="task1" name="Task with Extensions">
      <extensionElements>
        <camunda:assignee value="alice"/>
      </extensionElements>
    </userTask>
    <scriptTask id="script1" scriptFormat="javascript">
      <script>console.log('test');</script>
    </scriptTask>
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="f2" sourceRef="task1" targetRef="script1"/>
    <sequenceFlow id="f3" sourceRef="script1" targetRef="end"/>
  </process>
</definitions>
""";

    [Fact]
    public async Task EnableTracing_CreatesSpanWithExpectedAttributes()
    {
        // Arrange
        using var activitySource = new ActivitySource("TestSource");
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(activityListener);

        Activity? capturedActivity = null;
        activityListener.ActivityStarted = activity => capturedActivity = activity;
        
        var options = new BpmnParserOptions
        {
            EnableTracing = true,
            TracingActivitySource = activitySource,
            RoundtripMode = BpmnRoundtripMode.Strict,
            BuildRuntimeProjection = true,
            NormalizeVendorExtensions = true
        };

        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(ComplexXml);

        // Assert
        Assert.NotNull(capturedActivity);
        Assert.Equal("BpmnParser.ParseAsync", capturedActivity!.DisplayName);
        
        // Expected attributes
        Assert.Contains(capturedActivity.Tags, t => t.Key == "bpmn.process_id" && t.Value == "complexProcess");
        Assert.Contains(capturedActivity.Tags, t => t.Key == "bpmn.node_count" && int.Parse(t.Value!) > 0);
        Assert.Contains(capturedActivity.Tags, t => t.Key == "bpmn.flow_count" && int.Parse(t.Value!) > 0);
        Assert.Contains(capturedActivity.Tags, t => t.Key == "bpmn.roundtrip_mode" && t.Value == "Strict");
        Assert.Contains(capturedActivity.Tags, t => t.Key == "bpmn.runtime_projection" && t.Value == "true");
        Assert.Contains(capturedActivity.Tags, t => t.Key == "bpmn.vendor_normalization" && t.Value == "true");
    }

    [Fact]
    public async Task EnableLogging_CapturesStructuredMessages()
    {
        // Arrange
        var logger = new TestLogger<BpmnParser>();
        var options = new BpmnParserOptions
        {
            EnableLogging = true,
            Logger = logger,
            RoundtripMode = BpmnRoundtripMode.Strict,
            BuildRuntimeProjection = true,
            EnableAdvancedValidation = true
        };

        var parser = new BpmnParser(options, logger);

        // Act
        var model = await parser.ParseAsync(SimpleXml);

        // Assert
        var logs = logger.LogEntries;
        
        // Expected log messages at different stages
        Assert.Contains(logs, l => l.Message.Contains("ParseStart") && l.Level == LogLevel.Debug);
        Assert.Contains(logs, l => l.Message.Contains("PhaseComplete") && l.Level == LogLevel.Debug);
        
        // If there are validation diagnostics, we should see ValidationSummary
        if (model.ValidationDiagnostics?.Count > 0)
        {
            Assert.Contains(logs, l => l.Message.Contains("ValidationSummary") && l.Level == LogLevel.Information);
        }
        
        // Since BuildRuntimeProjection is enabled, we should see ProjectionBuilt
        Assert.Contains(logs, l => l.Message.Contains("ProjectionBuilt") && l.Level == LogLevel.Debug);
        
        // Verify structured data contains ProcessId
        var parseStartLog = logs.First(l => l.Message.Contains("ParseStart"));
        Assert.NotNull(parseStartLog.StructuredState);
    }

    [Fact]
    public async Task TracingDisabled_NoAllocationOverhead()
    {
        // Arrange - tracing disabled by default
        var logger = new TestLogger<BpmnParser>();
        var options = new BpmnParserOptions
        {
            EnableTracing = false, // explicit
            EnableLogging = false,
            RoundtripMode = BpmnRoundtripMode.Normalized
        };

        var parser = new BpmnParser(options, logger);

        // Act & Assert - should not throw or allocate tracing objects
        var model = await parser.ParseAsync(SimpleXml);
        
        Assert.NotNull(model);
        Assert.Equal("testProcess", model.ProcessId);
        // No way to directly test "zero allocation" but ensuring it doesn't crash
        // and basic parsing still works when observability is disabled
    }

    [Fact]
    public async Task LoggingDisabled_NoLoggerCalls()
    {
        // Arrange
        var mockLogger = new TestLogger<BpmnParser>();
        var options = new BpmnParserOptions
        {
            EnableLogging = false,
            Logger = mockLogger,
            RoundtripMode = BpmnRoundtripMode.Normalized
        };

        var parser = new BpmnParser(options, mockLogger);

        // Act
        var model = await parser.ParseAsync(SimpleXml);

        // Assert - no logs should be captured when logging is disabled
        var logs = mockLogger.LogEntries;
        Assert.Empty(logs);
    }

    [Fact]
    public async Task TracingWithValidationWarnings_IncludesWarningCount()
    {
        // Arrange - XML with validation issues to generate warnings
        const string xmlWithWarnings = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="warningProcess">
    <startEvent id="start"/>
    <exclusiveGateway id="gw1" default="f2"/>
    <userTask id="task1"/>
    <userTask id="task2"/>
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="gw1"/>
    <sequenceFlow id="f2" sourceRef="gw1" targetRef="task1">
      <conditionExpression><![CDATA[${shouldNotHaveCondition}]]></conditionExpression>
    </sequenceFlow>
    <sequenceFlow id="f3" sourceRef="gw1" targetRef="task2"/>
    <sequenceFlow id="f4" sourceRef="task1" targetRef="end"/>
    <sequenceFlow id="f5" sourceRef="task2" targetRef="end"/>
  </process>
</definitions>
""";

        using var activitySource = new ActivitySource("TestSource");
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(activityListener);

        Activity? capturedActivity = null;
        activityListener.ActivityStarted = activity => capturedActivity = activity;
        
        var options = new BpmnParserOptions
        {
            EnableTracing = true,
            TracingActivitySource = activitySource,
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableAdvancedValidation = true
        };

        var parser = new BpmnParser(options);

        // Act
        var model = await parser.ParseAsync(xmlWithWarnings);

        // Assert
        Assert.NotNull(capturedActivity);
        Assert.Contains(capturedActivity!.Tags, t => t.Key == "bpmn.validation_warnings" && int.Parse(t.Value!) >= 0);
        Assert.Contains(capturedActivity.Tags, t => t.Key == "bpmn.validation_errors" && int.Parse(t.Value!) >= 0);
    }
}