using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Bpmn;

[Collection("SharedSqliteDb")]
public class ProcessInstanceTests : IAsyncLifetime
{
    private readonly SharedSqliteDbFixture _fixture;
    private TestTransactionScope? _scope;
    private BpmnDbContext _ctx = null!;

    public ProcessInstanceTests(SharedSqliteDbFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _scope = new TestTransactionScope(_fixture);
        await _scope.InitializeAsync();
        _ctx = new BpmnDbContext(_fixture.BpmnOptions);
    }

    public async ValueTask DisposeAsync() => await _scope!.DisposeAsync();

    [Fact]
    public async Task CanQuerySeedProcessInstance()
    {
        var pi = await _ctx.ProcessInstances.FirstOrDefaultAsync(p => p.InstanceId == "sample-instance-1");
        Assert.NotNull(pi);
    }
}