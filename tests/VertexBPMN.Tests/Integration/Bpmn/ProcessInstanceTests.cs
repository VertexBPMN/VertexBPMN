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

    public  ValueTask InitializeAsync()
    {
        //_scope = new TestTransactionScope(_fixture);
        //await _scope.InitializeAsync();
        _ctx = new BpmnDbContext(_fixture.BpmnOptions);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task CanQuerySeedProcessInstance()
    {
        var processInstance = await _ctx.ProcessInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.InstanceId == "sample-instance-1");

        Assert.NotNull(processInstance);
    }
}