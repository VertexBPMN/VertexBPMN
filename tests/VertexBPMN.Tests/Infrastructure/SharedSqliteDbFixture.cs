using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Infrastructure;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public xUnit collection fixture retained for framework activation and injection into public test constructors.")]
public sealed class SharedSqliteDbFixture : IAsyncLifetime
{
	private bool _initialized;

	public SqliteConnection Connection { get; } = new("Data Source=:memory:;Mode=Memory;Cache=Shared");

	public DbContextOptions<BpmnDbContext> BpmnOptions => new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(Connection).Options;
	private readonly Dictionary<string, SqliteConnection> _auxiliaryConnections = new(StringComparer.Ordinal);
	public DbContextOptions<TenantDbContext> TenantOptions => new DbContextOptionsBuilder<TenantDbContext>().UseSqlite(GetConnection("tenants")).Options;
	public DbContextOptions<SimulationScenarioDbContext> SimulationOptions => new DbContextOptionsBuilder<SimulationScenarioDbContext>().UseSqlite(GetConnection("simulation")).Options;
	public DbContextOptions<DecisionDbContext> DecisionOptions => new DbContextOptionsBuilder<DecisionDbContext>().UseSqlite(GetConnection("decision")).Options;
	public DbContextOptions<ProcessMiningEventDbContext> ProcessMiningOptions => new DbContextOptionsBuilder<ProcessMiningEventDbContext>().UseSqlite(GetConnection("procmining")).Options;

	internal SqliteConnection GetConnection(string label)
	{
		if (!_auxiliaryConnections.TryGetValue(label, out var connection))
		{
			connection = new SqliteConnection("Data Source=:memory:");
			connection.Open();
			_auxiliaryConnections.Add(label, connection);
		}
		return connection;
	}

	public async ValueTask InitializeAsync()
	{
		if (_initialized)
		{
			return;
		}
		await Connection.OpenAsync();
		await CreateCurrentSchemaAsync();
		await SeedSampleInstanceAsync();
		_initialized = true;
	}

	public async ValueTask DisposeAsync()
	{
		foreach (var connection in _auxiliaryConnections.Values)
		{
			await connection.DisposeAsync();
		}
		await Connection.DisposeAsync();
	}

	private async Task CreateCurrentSchemaAsync()
	{
		// Separate context databases mirror the application's persistence boundaries and
		// avoid EnsureCreated silently skipping tables after another context creates its schema.
		await using var bpmn = new BpmnDbContext(BpmnOptions);
		await using var tenants = new TenantDbContext(TenantOptions);
		await using var simulation = new SimulationScenarioDbContext(SimulationOptions);
		await using var decisions = new DecisionDbContext(DecisionOptions);
		await using var mining = new ProcessMiningEventDbContext(ProcessMiningOptions);
		await bpmn.Database.MigrateAsync();
		await tenants.Database.EnsureCreatedAsync();
		await simulation.Database.EnsureCreatedAsync();
		await decisions.Database.EnsureCreatedAsync();
		await mining.Database.EnsureCreatedAsync();
	}

	private async Task SeedSampleInstanceAsync()
	{
		await using var db = new BpmnDbContext(BpmnOptions);
		if (await db.ProcessInstances.AnyAsync(instance => instance.InstanceId == "sample-instance-1"))
		{
			return;
		}
		var timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var definitionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		var definition = await db.ProcessDefinitions.SingleOrDefaultAsync(item => item.Id == definitionId)
			?? CreateSampleDefinition(timestamp);
		db.ProcessInstances.Add(new ProcessInstance
		{
			Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
			ProcessDefinition = definition,
			BusinessKey = "BK-001",
			InstanceId = "sample-instance-1",
			ProcessId = definition.Key,
			State = "Running",
			StartedAt = timestamp,
			Revision = 1
		});
		await db.SaveChangesAsync();
	}

	private static ProcessDefinition CreateSampleDefinition(DateTime timestamp)
	{
		var deployment = new EngineDeployment
		{
			Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
			Name = "SampleDeployment",
			CreatedAt = timestamp
		};
		return new ProcessDefinition
		{
			Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
			Key = "SampleProcess",
			Name = "Sample Process",
			Version = 1,
			BpmnXml = """<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"><process id="SampleProcess"><startEvent id="start"/><sequenceFlow id="flow" sourceRef="start" targetRef="end"/><endEvent id="end"/></process></definitions>""",
			CreatedAt = timestamp,
			Deployment = deployment
		};
	}
}
