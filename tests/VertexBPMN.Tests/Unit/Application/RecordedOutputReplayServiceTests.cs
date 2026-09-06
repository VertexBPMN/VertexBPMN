using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class RecordedOutputReplayServiceTests
{
    private sealed class FakeQuery(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> outputs)
        : IRecordedOutputQueryService
    {
        public Task<IReadOnlyDictionary<string, object>?> GetLastRecordedOutputAsync(
            string tenantId, string processDefinitionKey, string elementId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(outputs.TryGetValue(elementId, out var o) ? o : null);
    }

    [Fact]
    public async Task RewriteForReplay_RewritesRecordedTasks_And_RegistersReplayingHandler()
    {
        var registry = new ServiceTaskRegistry();
        var query = new FakeQuery(new Dictionary<string, IReadOnlyDictionary<string, object>>
        {
            ["callApi"] = new Dictionary<string, object> { ["apiToken"] = "abc", ["result"] = 42 }
        });
        var service = new RecordedOutputReplayService(query, registry);

        var model = new BpmnModel(
            "orderProc", "Order",
            Tasks: new List<BpmnTask>
            {
                new("callApi", "serviceTask", Attributes: new Dictionary<string, string>
                {
                    ["vertex:connector.type"] = "http"
                }, Implementation: "http"),
                new("manualStep", "serviceTask", Implementation: "http")
            });

        var rewritten = await service.RewriteForReplayAsync("t1", "orderProc", model);

        // Recorded task is re-routed to a synthetic replay implementation.
        Assert.Equal("__replay__:callApi", rewritten.Tasks![0].Implementation);
        // Task without a prior snapshot keeps its live connector implementation.
        Assert.Equal("http", rewritten.Tasks![1].Implementation);

        // The replay handler writes the recorded outputs into variables.
        var handler = registry.GetHandler("__replay__:callApi");
        var variables = new Dictionary<string, object>();
        await handler.ExecuteAsync(new Dictionary<string, string>(), variables);

        Assert.Equal("abc", variables["apiToken"]);
        Assert.Equal(42, variables["result"]);
    }

    [Fact]
    public async Task RewriteForReplay_NonServiceTasks_AreUntouched()
    {
        var registry = new ServiceTaskRegistry();
        var service = new RecordedOutputReplayService(new FakeQuery(
            new Dictionary<string, IReadOnlyDictionary<string, object>>()), registry);

        var model = new BpmnModel("p", "p", Tasks: new List<BpmnTask>
        {
            new("user_1", "userTask")
        });

        var rewritten = await service.RewriteForReplayAsync("t1", "p", model);

        // No recorded outputs exist; the task element is preserved as-is.
        Assert.Same(model.Tasks![0], rewritten.Tasks![0]);
        Assert.Equal("userTask", rewritten.Tasks![0].Type);
    }
}
