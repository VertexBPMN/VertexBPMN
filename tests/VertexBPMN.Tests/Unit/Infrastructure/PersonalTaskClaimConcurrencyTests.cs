using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Repositories;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// Proves that concurrent claim attempts on the same task resolve to exactly one
/// winner under a real race. Every claim executes on its own connection to a shared
/// in-memory SQLite database, so the CAS attempts genuinely race on separate
/// connections and are serialized only by the database write lock.
/// </summary>
public sealed class PersonalTaskClaimConcurrencyTests : IAsyncLifetime
{
	private readonly string _connectionString;
	private SqliteConnection? _anchor;
	private Guid _processId;

	public PersonalTaskClaimConcurrencyTests()
	{
		// Shared in-memory cache: each context/connection sees the same database while
		// owning an independent connection, enabling true parallel write attempts.
		_connectionString = $"Data Source=claim-race-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False;Default Timeout=30";
		_anchor = null;
	}

	public async ValueTask InitializeAsync()
	{
		_anchor = new SqliteConnection(_connectionString);
		await _anchor.OpenAsync();
		var options = new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(_connectionString).Options;
		await using var db = new BpmnDbContext(options);
		await db.Database.EnsureCreatedAsync();

		var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "claim", TenantId = "tenant-a" };
		var definition = new ProcessDefinition
		{
			Id = Guid.NewGuid(),
			Key = "claim",
			Name = "claim",
			DeploymentId = deployment.Id,
			TenantId = "tenant-a",
			TenantScope = "tenant-a",
			Version = 1
		};
		var process = new ProcessInstance { Id = Guid.NewGuid(), ProcessDefinitionId = definition.Id, TenantId = "tenant-a" };
		db.EngineDeployments.Add(deployment);
		db.ProcessDefinitions.Add(definition);
		db.ProcessInstances.Add(process);
		await db.SaveChangesAsync();
		_processId = process.Id;
	}

	public async ValueTask DisposeAsync()
	{
		if (_anchor is not null)
		{
			await _anchor.DisposeAsync();
		}
	}

	private DbContextOptions<BpmnDbContext> Options()
		=> new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(_connectionString).Options;

	private async Task<Guid> SeedTaskAsync(long revision = 1)
	{
		await using var db = new BpmnDbContext(Options());
		var task = new UserTask
		{
			Id = Guid.NewGuid(),
			TenantId = "tenant-a",
			Name = "Approve",
			ActivityId = "approve",
			Type = "userTask",
			Status = UserTaskStatus.Pending,
			Revision = revision,
			CreatedAt = DateTime.UtcNow,
			ProcessInstanceId = _processId,
		};
		db.Tasks.Add(task);
		await db.SaveChangesAsync();
		return task.Id;
	}

	private async Task<TaskClaimOutcome> ClaimAsync(Guid taskId, long expectedRevision, string caller)
	{
		await using var db = new BpmnDbContext(Options());
		var repo = new TaskRepository(db);
		return await repo.TryClaimAsync(
			new TaskClaimAttempt(taskId, caller, "tenant-a", expectedRevision),
			TestContext.Current.CancellationToken);
	}

	private static readonly TaskClaimOutcome[] LostOutcomes =
		[TaskClaimOutcome.StaleRevision, TaskClaimOutcome.AlreadyAssigned];

	[Fact]
	public async Task TwoConcurrentClaims_SameTask_ExactlyOneWinner()
	{
		var taskId = await SeedTaskAsync(revision: 1);

		var results = await Task.WhenAll(ClaimAsync(taskId, 1, "u1"), ClaimAsync(taskId, 1, "u2"));

		Assert.Equal(1, results.Count(r => r == TaskClaimOutcome.Claimed));
		Assert.Equal(1, results.Count(r => LostOutcomes.Contains(r)));

		await using var read = new BpmnDbContext(Options());
		var final = await read.Tasks.SingleAsync(t => t.Id == taskId, TestContext.Current.CancellationToken);
		Assert.True(final.Assignee is "u1" or "u2", $"assignee was {final.Assignee}");
		Assert.Equal(2, final.Revision); // revision bumped exactly once
	}

	[Fact]
	public async Task ManyConcurrentClaims_SameTask_ExactlyOneWinner()
	{
		var taskId = await SeedTaskAsync(revision: 1);
		var callers = Enumerable.Range(0, 32).Select(i => $"u{i}").ToArray();

		// Start-gate every claim so they genuinely race: each context reads the same
		// initial revision before any of them commits, forcing real read/write overlap.
		using var gate = new ManualResetEventSlim(false);
		var attempts = callers.Select(c => Task.Run(() =>
		{
			gate.Wait();
			return ClaimAsync(taskId, 1, c);
		})).ToArray();
		gate.Set();
		var results = await Task.WhenAll(attempts);

		Assert.Equal(1, results.Count(r => r == TaskClaimOutcome.Claimed));
		Assert.Equal(results.Length - 1, results.Count(r => LostOutcomes.Contains(r)));

		await using var read = new BpmnDbContext(Options());
		var final = await read.Tasks.SingleAsync(t => t.Id == taskId, TestContext.Current.CancellationToken);
		Assert.Contains(final.Assignee, callers, StringComparer.Ordinal);
		Assert.Equal(2, final.Revision); // revision bumped exactly once
	}
}
