using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Bpmn;

[Collection("SharedSqliteDb")]
public class ProcessInstanceTests(SharedSqliteDbFixture fixture) : IAsyncLifetime
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213", Justification = "Borrowed xUnit collection fixture is disposed by xUnit after the collection, not by an individual test instance.")]
	private readonly SharedSqliteDbFixture _fixture = fixture;
	private BpmnDbContext _ctx = null!;

	public ValueTask InitializeAsync()
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
			.FirstOrDefaultAsync(p => p.InstanceId == "sample-instance-1", cancellationToken: TestContext.Current.CancellationToken);

		Assert.NotNull(processInstance);
	}
}
