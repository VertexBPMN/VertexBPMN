using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Repositories;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class PersonalTaskClaimRepositoryTests : IDisposable
{
	private readonly SqliteConnection _connection;
	private readonly BpmnDbContext _db;
	private readonly TaskRepository _repo;
	private readonly Guid _processId;

	private static void SeedProcessFixture(BpmnDbContext db, Guid processId)
	{
		var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "inbox", TenantId = "tenant-a" };
		var definition = new ProcessDefinition
		{
			Id = Guid.NewGuid(),
			Key = "inbox",
			Name = "inbox",
			DeploymentId = deployment.Id,
			TenantId = "tenant-a",
			TenantScope = "tenant-a",
			Version = 1
		};
		db.EngineDeployments.Add(deployment);
		db.ProcessDefinitions.Add(definition);
		db.ProcessInstances.Add(new ProcessInstance { Id = processId, ProcessDefinitionId = definition.Id, TenantId = "tenant-a" });
	}

	public PersonalTaskClaimRepositoryTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		_db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(_connection).Options);
		_db.Database.EnsureCreated();
		_processId = Guid.NewGuid();
		SeedProcessFixture(_db, _processId);
		_db.SaveChanges();
		_repo = new TaskRepository(_db);
	}

	private UserTask Seed(
		Guid? id = null,
		string? tenantId = "tenant-a",
		string? assignee = null,
		UserTaskStatus status = UserTaskStatus.Pending,
		long revision = 1)
	{
		var task = new UserTask
		{
			Id = id ?? Guid.NewGuid(),
			TenantId = tenantId,
			Name = "Approve",
			ActivityId = "approve",
			Type = "userTask",
			Assignee = assignee,
			Status = status,
			Revision = revision,
			CreatedAt = DateTime.UtcNow,
			ProcessInstanceId = _processId,
		};
		_db.Tasks.Add(task);
		_db.SaveChanges();
		return task;
	}

	public void Dispose()
	{
		_db.Dispose();
		_connection.Dispose();
	}

	private async Task<UserTask?> Latest(Guid id) => await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id);

	[Fact]
	public async Task TryClaim_UnassignedPendingTask_AssignsCallerAndAdvancesRevision()
	{
		var task = Seed(revision: 4);

		var outcome = await _repo.TryClaimAsync(new TaskClaimAttempt(task.Id, "u1", "tenant-a", 4), TestContext.Current.CancellationToken);

		Assert.Equal(TaskClaimOutcome.Claimed, outcome);
		var latest = await Latest(task.Id);
		Assert.Equal("u1", latest!.Assignee);
		Assert.Equal(5, latest.Revision);
	}

	[Fact]
	public async Task TryClaim_StaleRevision_ReturnsStaleRevisionWithoutMutation()
	{
		var task = Seed(revision: 4);

		var outcome = await _repo.TryClaimAsync(new TaskClaimAttempt(task.Id, "u1", "tenant-a", 3), TestContext.Current.CancellationToken);

		Assert.Equal(TaskClaimOutcome.StaleRevision, outcome);
		Assert.Null((await Latest(task.Id))!.Assignee);
	}

	[Fact]
	public async Task TryClaim_CompletedTask_ReturnsWrongState()
	{
		var task = Seed(status: UserTaskStatus.Completed);

		var outcome = await _repo.TryClaimAsync(new TaskClaimAttempt(task.Id, "u1", "tenant-a", task.Revision), TestContext.Current.CancellationToken);

		Assert.Equal(TaskClaimOutcome.WrongState, outcome);
		Assert.Null((await Latest(task.Id))!.Assignee);
	}

	[Fact]
	public async Task TryClaim_UnassignedDelegatedTask_Claims()
	{
		var task = Seed(status: UserTaskStatus.Delegated, revision: 3);

		var outcome = await _repo.TryClaimAsync(new TaskClaimAttempt(task.Id, "u1", "tenant-a", 3), TestContext.Current.CancellationToken);

		Assert.Equal(TaskClaimOutcome.Claimed, outcome);
		var latest = await Latest(task.Id);
		Assert.Equal("u1", latest!.Assignee);
		Assert.Equal(4, latest.Revision);
	}

	[Fact]
	public async Task TryClaim_AlreadyAssignedToAnother_ReturnsAlreadyAssigned()
	{
		var task = Seed(assignee: "rival");

		var outcome = await _repo.TryClaimAsync(new TaskClaimAttempt(task.Id, "u1", "tenant-a", task.Revision), TestContext.Current.CancellationToken);

		Assert.Equal(TaskClaimOutcome.AlreadyAssigned, outcome);
		Assert.Equal("rival", (await Latest(task.Id))!.Assignee);
	}

	[Fact]
	public async Task TryClaim_ReclaimOwnTask_IsIdempotent()
	{
		var task = Seed(assignee: "u1", revision: 2);

		var outcome = await _repo.TryClaimAsync(new TaskClaimAttempt(task.Id, "u1", "tenant-a", 2), TestContext.Current.CancellationToken);

		Assert.Equal(TaskClaimOutcome.Claimed, outcome);
	}

	[Fact]
	public async Task TryClaim_OtherTenant_ReturnsNotFound()
	{
		var task = Seed(tenantId: "tenant-b");

		var outcome = await _repo.TryClaimAsync(new TaskClaimAttempt(task.Id, "u1", "tenant-a", task.Revision), TestContext.Current.CancellationToken);

		Assert.Equal(TaskClaimOutcome.NotFound, outcome);
		Assert.Null((await Latest(task.Id))!.Assignee);
	}
}
