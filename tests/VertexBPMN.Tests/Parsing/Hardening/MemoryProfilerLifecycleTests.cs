using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Engine.Security;

namespace VertexBPMN.Tests.Parsing.Hardening;

// Absolute heap measurements must not overlap other collections. This changes
// scheduling, not the measured scope, assertions, or CI inclusion of the tests.
[CollectionDefinition("ProcessMemoryMeasurements", DisableParallelization = true)]
public sealed class ProcessMemoryMeasurementsCollection { }

[Collection("ProcessMemoryMeasurements")]
public sealed class MemoryProfilerLifecycleTests
{
    private const string Xml = """
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
          <process id="profile"><startEvent id="start"/><endEvent id="end"/>
            <sequenceFlow id="flow" sourceRef="start" targetRef="end"/>
          </process>
        </definitions>
        """;

    [Fact]
    public async Task Failure_is_propagated_and_subsequent_profile_completes()
    {
        var profiler = new BpmnMemoryProfiler();
        await Assert.ThrowsAsync<SecurityException>(() => profiler.ProfileParseOperationAsync(
            "<definitions>", new BpmnParserOptions(), TestContext.Current.CancellationToken));
        var snapshot = await profiler.ProfileParseOperationAsync(Xml, new BpmnParserOptions(),
            TestContext.Current.CancellationToken);
        Assert.Equal(3, snapshot.ElementCount);
        Assert.True(snapshot.TotalAllocatedMB > 0);
        Assert.True(snapshot.PeakMemoryUsageMB >= snapshot.InitialMemoryUsageMB);
        Assert.True(snapshot.PeakMemoryUsageMB >= snapshot.FinalMemoryUsageMB);
    }

    [Fact]
    public async Task Precancelled_profile_preserves_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BpmnMemoryProfiler()
            .ProfileParseOperationAsync(Xml, new BpmnParserOptions(), cancellation.Token));
    }
}
