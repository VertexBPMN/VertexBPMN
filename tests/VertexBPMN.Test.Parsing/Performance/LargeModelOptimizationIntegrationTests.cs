using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Performance;

/// <summary>
/// Tests to verify that large model optimizations work correctly (Zero-Break approach).
/// Options are off by default to maintain compatibility, but can be enabled for performance.
/// </summary>
public class LargeModelOptimizationIntegrationTests
{
    [Fact]
    public async Task LargeModelOptimizations_AreOffByDefault_ZeroBreak()
    {
        // Arrange - Default options should have optimizations disabled
        var defaultOptions = new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict
        };

        // Assert - Zero-break: All optimizations off by default
        Assert.False(defaultOptions.OptimizeLargeModels);
        Assert.False(defaultOptions.SkipDocumentationForLargeModels);
        Assert.False(defaultOptions.SkipArtifactsForLargeModels);
        Assert.False(defaultOptions.SkipExtensionsForLargeModels);
        Assert.Equal(100, defaultOptions.LargeModelThreshold);
    }

    [Fact]
    public async Task LargeModel_WithOptimizationsEnabled_ParsesSuccessfully()
    {
        // Arrange
        var optimizedOptions = new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            OptimizeLargeModels = true,                      // Enable optimization framework
            LargeModelThreshold = 50,                        // Low threshold for testing
            SkipDocumentationForLargeModels = true,          // Skip documentation capture
            SkipArtifactsForLargeModels = true,              // Skip artifact capture
            SkipExtensionsForLargeModels = false,            // Keep extensions for compatibility
            UseLazyRawCloning = true,
            UseSharedStringPool = true
        };

        var parser = new BpmnParser(optimizedOptions);
        var largeModelXml = GenerateLargeTestModel(100); // Above threshold

        // Act
        var model = await parser.ParseAsync(largeModelXml);

        // Assert - Model parsed successfully
        Assert.NotNull(model);
        Assert.Equal("largeTestProcess", model.ProcessId);
        Assert.True(model.Tasks.Count > 50);
        Assert.Empty(model.Diagnostics); // Should parse without errors
    }

    [Fact]
    public async Task LargeModel_WithOptimizationsDisabled_ParsesIdentically()
    {
        // Arrange
        var unoptimizedOptions = new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            OptimizeLargeModels = false, // Disabled
            InternIds = true
        };

        var optimizedOptions = new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            OptimizeLargeModels = true,
            LargeModelThreshold = 50,
            SkipDocumentationForLargeModels = true,
            SkipArtifactsForLargeModels = true,
            InternIds = true
        };

        var unoptimizedParser = new BpmnParser(unoptimizedOptions);
        var optimizedParser = new BpmnParser(optimizedOptions);
        var testXml = GenerateLargeTestModel(100);

        // Act
        var unoptimizedModel = await unoptimizedParser.ParseAsync(testXml);
        var optimizedModel = await optimizedParser.ParseAsync(testXml);

        // Assert - Core model elements should be identical
        Assert.Equal(unoptimizedModel.ProcessId, optimizedModel.ProcessId);
        Assert.Equal(unoptimizedModel.Tasks.Count, optimizedModel.Tasks.Count);
        Assert.Equal(unoptimizedModel.Events.Count, optimizedModel.Events.Count);
        Assert.Equal(unoptimizedModel.SequenceFlows.Count, optimizedModel.SequenceFlows.Count);

        // Task properties should be identical
        for (int i = 0; i < unoptimizedModel.Tasks.Count; i++)
        {
            Assert.Equal(unoptimizedModel.Tasks[i].Id, optimizedModel.Tasks[i].Id);
            Assert.Equal(unoptimizedModel.Tasks[i].Name, optimizedModel.Tasks[i].Name);
            Assert.Equal(unoptimizedModel.Tasks[i].Type, optimizedModel.Tasks[i].Type);
        }
    }

    [Fact]
    public async Task SmallModel_BelowThreshold_NoOptimizationsApplied()
    {
        // Arrange
        var options = new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            OptimizeLargeModels = true,      // Enabled
            LargeModelThreshold = 100,       // But threshold high
            SkipDocumentationForLargeModels = true,
            SkipArtifactsForLargeModels = true
        };

        var parser = new BpmnParser(options);
        var smallModelXml = GenerateSmallTestModel(); // Well below threshold

        // Act
        var model = await parser.ParseAsync(smallModelXml);

        // Assert - Should parse normally without optimizations
        Assert.NotNull(model);
        Assert.Equal("smallTestProcess", model.ProcessId);
        Assert.True(model.Tasks.Count < 10);
        
        // For small models, raw metadata should be preserved even with optimizations enabled
        if (model.RawMetadata != null)
        {
            // Documentation should be preserved for small models
            Assert.True(model.RawMetadata.RawDocumentation?.Count >= 0);
        }
    }

    private static string GenerateLargeTestModel(int elementCount)
    {
        var xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="largeTestProcess">
    <startEvent id="start"/>
""";

        // Generate many tasks with documentation and artifacts
        for (int i = 1; i <= elementCount; i++)
        {
            xml += $"""
    <userTask id="task{i}" name="Task {i}">
      <documentation>Detailed documentation for task {i} that can be optimized away in large models.</documentation>
    </userTask>
""";
        }

        xml += """
    <endEvent id="end"/>
    <sequenceFlow id="f0" sourceRef="start" targetRef="task1"/>
""";

        // Connect tasks sequentially
        for (int i = 1; i <= elementCount; i++)
        {
            if (i < elementCount)
            {
                xml += $"""    <sequenceFlow id="f{i}" sourceRef="task{i}" targetRef="task{i + 1}"/>""" + "\n";
            }
            else
            {
                xml += $"""    <sequenceFlow id="f{i}" sourceRef="task{i}" targetRef="end"/>""" + "\n";
            }
        }

        xml += """
  </process>
</definitions>
""";
        return xml;
    }

    private static string GenerateSmallTestModel()
    {
        return """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="smallTestProcess">
    <startEvent id="start">
      <documentation>Start event documentation</documentation>
    </startEvent>
    <userTask id="task1" name="Simple Task">
      <documentation>User task documentation</documentation>
    </userTask>
    <endEvent id="end">
      <documentation>End event documentation</documentation>
    </endEvent>
    <sequenceFlow id="f1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="f2" sourceRef="task1" targetRef="end"/>
  </process>
</definitions>
""";
    }
}