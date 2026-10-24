using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Repositories;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class PersonalTaskQueryRepositoryTests : IDisposable
{
	private static readonly string[] ApproverRole = ["approver"];

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

	public PersonalTaskQueryRepositoryTests()
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
		string? name = "Approve",
		string? assignee = null,
		List<string>? candidates = null,
		string? candidateRole = null,
		UserTaskStatus status = UserTaskStatus.Pending,
		DateTime? createdAt = null)
	{
		var task = new UserTask
		{
			Id = id ?? Guid.NewGuid(),
			TenantId = tenantId,
			Name = name ?? "Approve",
			ActivityId = "approve",
			Type = "userTask",
			Assignee = assignee,
			CandidateUsers = candidates ?? new List<string>(),
			CandidateRole = candidateRole ?? string.Empty,
			Status = status,
			CreatedAt = createdAt ?? DateTime.UtcNow,
			ProcessInstanceId = _processId,
			LocalVariables = new Dictionary<string, object>(StringComparer.Ordinal) { ["secret"] = "value" },
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

	[Fact]
	public async Task QueryInbox_IsBoundedToTenantAndAssigneeOrCandidate()
	{
		var tenantA = Seed(name: "A-assigned", assignee: "u1");
		Seed(tenantId: "tenant-b", name: "B-assigned", assignee: "u1");
		Seed(name: "A-candidate", candidates: new List<string> { "u1" });
		Seed(name: "A-unrelated", assignee: "someone-else");

		var result = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u1", Array.Empty<string>()), TestContext.Current.CancellationToken);

		var ids = result.Items.Select(i => i.Id).OrderBy(x => x).ToList();
		Assert.Contains(tenantA.Id, ids);
		Assert.Equal(2, result.Items.Count);
		Assert.All(result.Items, i => Assert.Equal("tenant-a", i.TenantId));
	}

	[Fact]
	public async Task QueryInbox_CandidateRole_MatchingUserRoleIsVisible()
	{
		Seed(name: "role-task", candidateRole: "approver");

		var result = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u9", ApproverRole), TestContext.Current.CancellationToken);

		var row = Assert.Single(result.Items);
		Assert.Equal("role-task", row.Name);
	}

	[Fact]
	public async Task QueryInbox_NonCandidateWithNoMatchingRole_IsExcluded()
	{
		Seed(name: "role-task", candidateRole: "other-role");

		var result = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u9", ApproverRole), TestContext.Current.CancellationToken);

		Assert.Empty(result.Items);
	}

	[Fact]
	public async Task QueryInbox_SearchFiltersOnName()
	{
		Seed(name: "Invoice approval", assignee: "u1");
		Seed(name: "Contract signing", assignee: "u1");

		var result = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u1", Array.Empty<string>(), Search: "Invoice"), TestContext.Current.CancellationToken);

		var row = Assert.Single(result.Items);
		Assert.Equal("Invoice approval", row.Name);
	}

	[Fact]
	public async Task QueryInbox_ClosedTasksAreNotActionable()
	{
		Seed(name: "done", assignee: "u1", status: UserTaskStatus.Completed);
		Seed(name: "open", assignee: "u1");

		var result = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u1", Array.Empty<string>()), TestContext.Current.CancellationToken);

		var row = Assert.Single(result.Items);
		Assert.Equal("open", row.Name);
	}

	[Fact]
	public async Task QueryInbox_PaginationCapsPageSizeAndAppliesOffset()
	{
		for (var i = 0; i < 5; i++)
		{
			Seed(name: $"t{i}", assignee: "u1", createdAt: DateTime.UtcNow.AddSeconds(i));
		}

		var page1 = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u1", Array.Empty<string>(), Page: 1, PageSize: 2), TestContext.Current.CancellationToken);
		Assert.Equal(2, page1.Items.Count);
		Assert.Equal(5, page1.Total);

		var page2 = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u1", Array.Empty<string>(), Page: 2, PageSize: 2), TestContext.Current.CancellationToken);
		Assert.Equal(2, page2.Items.Count);
		Assert.Equal(2, page2.Page);

		var page3 = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u1", Array.Empty<string>(), Page: 3, PageSize: 2), TestContext.Current.CancellationToken);
		Assert.Single(page3.Items);
	}

	[Fact]
	public async Task QueryInbox_PageSizeIsClampedToMaximum()
	{
		Seed(name: "a", assignee: "u1");

		var result = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u1", Array.Empty<string>(), PageSize: 1000), TestContext.Current.CancellationToken);

		Assert.Equal(TaskInboxQuery.MaxPageSize, result.PageSize);
	}

	[Fact]
	public async Task QueryInbox_FixedSortCreatedAscendingWithIdTiebreaker()
	{
		var now = DateTime.UtcNow;
		// Same CreatedAt: order is deterministic by Id ascending.
		var a = Seed(name: "alpha", assignee: "u1", createdAt: now);
		var b = Seed(name: "bravo", assignee: "u1", createdAt: now);

		var result = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u1", Array.Empty<string>()), TestContext.Current.CancellationToken);

		Assert.Equal(2, result.Items.Count);
		var ordered = result.Items.OrderBy(i => i.Id).Select(i => i.Id).ToList();
		Assert.Equal(ordered, result.Items.Select(i => i.Id).ToList());
		_ = a; _ = b;
	}

	[Fact]
	public async Task QueryInbox_ReturnsMinimizedProjectionWithoutVariables()
	{
		Seed(name: "projected", assignee: "u1");

		var result = await _repo.QueryInboxAsync(new TaskInboxQuery("tenant-a", "u1", Array.Empty<string>()), TestContext.Current.CancellationToken);

		// The inbox surface is a minimized TaskInboxRow: variables and internal fields
		// are not part of the projection contract.
		var row = Assert.Single(result.Items);
		Assert.IsType<TaskInboxRow>(row);
		Assert.Equal("projected", row.Name);
		Assert.Equal(UserTaskStatus.Pending, row.Status);
	}
}
