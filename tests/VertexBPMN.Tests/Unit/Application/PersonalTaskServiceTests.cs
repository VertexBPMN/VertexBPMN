using Moq;
using VertexBPMN.Application.PersonalTasks;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class PersonalTaskServiceTests
{
	private static readonly string[] ApproverRole = ["approver"];

	private static UserTask Task(
		Guid id,
		string tenant = "tenant-a",
		string? assignee = null,
		List<string>? candidates = null,
		string? candidateRole = null,
		UserTaskStatus status = UserTaskStatus.Pending,
		long revision = 1)
		=> new()
		{
			Id = id,
			TenantId = tenant,
			Name = "Approve",
			ActivityId = "approve",
			Type = "userTask",
			Assignee = assignee,
			CandidateUsers = candidates ?? new List<string>(),
			CandidateRole = candidateRole ?? string.Empty,
			Status = status,
			Revision = revision,
			CreatedAt = DateTime.UtcNow,
			ProcessInstanceId = Guid.NewGuid(),
		};

	private static (PersonalTaskService Service, Mock<ITaskRepository> Repo) Build(UserTask task)
	{
		var repo = new Mock<ITaskRepository>();
		repo.Setup(r => r.GetByIdAsync(task.Id, TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult<UserTask?>(task));
		return (new PersonalTaskService(repo.Object), repo);
	}

	[Fact]
	public async Task Claim_AssignedToSelf_Claims()
	{
		var id = Guid.NewGuid();
		var (service, repo) = Build(Task(id, assignee: "u1", revision: 3));
		repo.Setup(r => r.TryClaimAsync(It.IsAny<TaskClaimAttempt>(), TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult(TaskClaimOutcome.Claimed));

		var result = await service.ClaimForSelfAsync(id, "tenant-a", "u1", Array.Empty<string>(), TestContext.Current.CancellationToken);

		Assert.Equal(PersonalClaimOutcome.Claimed, result.Outcome);
		repo.Verify(r => r.TryClaimAsync(new TaskClaimAttempt(id, "u1", "tenant-a", 3), TestContext.Current.CancellationToken), Times.Once);
	}

	[Fact]
	public async Task Claim_CandidateByUserList_Claims()
	{
		var id = Guid.NewGuid();
		var (service, repo) = Build(Task(id, candidates: new List<string> { "u5" }));
		repo.Setup(r => r.TryClaimAsync(It.IsAny<TaskClaimAttempt>(), TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult(TaskClaimOutcome.Claimed));

		var result = await service.ClaimForSelfAsync(id, "tenant-a", "u5", Array.Empty<string>(), TestContext.Current.CancellationToken);

		Assert.Equal(PersonalClaimOutcome.Claimed, result.Outcome);
	}

	[Fact]
	public async Task Claim_CandidateByRole_Claims()
	{
		var id = Guid.NewGuid();
		var (service, repo) = Build(Task(id, candidateRole: "approver"));
		repo.Setup(r => r.TryClaimAsync(It.IsAny<TaskClaimAttempt>(), TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult(TaskClaimOutcome.Claimed));

		var result = await service.ClaimForSelfAsync(id, "tenant-a", "u9", ApproverRole, TestContext.Current.CancellationToken);

		Assert.Equal(PersonalClaimOutcome.Claimed, result.Outcome);
	}

	[Fact]
	public async Task Claim_NonCandidate_ReturnsNotCandidateWithoutRepoWrite()
	{
		var id = Guid.NewGuid();
		var (service, repo) = Build(Task(id, assignee: "rival", candidates: new List<string> { "other" }));

		var result = await service.ClaimForSelfAsync(id, "tenant-a", "u1", Array.Empty<string>(), TestContext.Current.CancellationToken);

		Assert.Equal(PersonalClaimOutcome.NotCandidate, result.Outcome);
		repo.Verify(r => r.TryClaimAsync(It.IsAny<TaskClaimAttempt>(), TestContext.Current.CancellationToken), Times.Never);
	}

	[Fact]
	public async Task Claim_StaleRevision_MapsToStaleRevision()
	{
		var id = Guid.NewGuid();
		var (service, repo) = Build(Task(id, candidateRole: "approver", revision: 9));
		repo.Setup(r => r.TryClaimAsync(It.IsAny<TaskClaimAttempt>(), TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult(TaskClaimOutcome.StaleRevision));

		var result = await service.ClaimForSelfAsync(id, "tenant-a", "u1", ApproverRole, TestContext.Current.CancellationToken);

		Assert.Equal(PersonalClaimOutcome.StaleRevision, result.Outcome);
	}

	[Fact]
	public async Task Claim_OtherTenant_ReturnsNotFound()
	{
		var id = Guid.NewGuid();
		var (service, repo) = Build(Task(id, tenant: "tenant-b"));

		var result = await service.ClaimForSelfAsync(id, "tenant-a", "u1", Array.Empty<string>(), TestContext.Current.CancellationToken);

		Assert.Equal(PersonalClaimOutcome.NotFound, result.Outcome);
		repo.Verify(r => r.TryClaimAsync(It.IsAny<TaskClaimAttempt>(), TestContext.Current.CancellationToken), Times.Never);
	}

	[Fact]
	public async Task Detail_ForAssignee_ReturnsMinimizedRow()
	{
		var id = Guid.NewGuid();
		var (service, _) = Build(Task(id, assignee: "u1"));

		var row = await service.GetVisibleDetailAsync(id, "tenant-a", "u1", Array.Empty<string>(), TestContext.Current.CancellationToken);

		Assert.NotNull(row);
		Assert.Equal(id, row.Id);
		Assert.Equal(UserTaskStatus.Pending, row.Status);
	}

	[Fact]
	public async Task Detail_ForNonCandidateReturnsNull()
	{
		var id = Guid.NewGuid();
		var (service, _) = Build(Task(id, assignee: "rival", candidates: new List<string> { "other" }));

		Assert.Null(await service.GetVisibleDetailAsync(id, "tenant-a", "u1", Array.Empty<string>(), TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task Detail_OtherTenant_ReturnsNull()
	{
		var id = Guid.NewGuid();
		var (service, _) = Build(Task(id, tenant: "tenant-b", assignee: "u1"));

		Assert.Null(await service.GetVisibleDetailAsync(id, "tenant-a", "u1", Array.Empty<string>(), TestContext.Current.CancellationToken));
	}
}
