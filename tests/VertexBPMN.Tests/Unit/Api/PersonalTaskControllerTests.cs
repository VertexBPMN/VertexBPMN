using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Api.Dto;
using VertexBPMN.Application.PersonalTasks;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class PersonalTaskControllerTests
{
	private static readonly Guid TaskId = Guid.NewGuid();

	private static PersonalTaskController Controller(
		Mock<IPersonalTaskService> personal,
		Mock<ITaskService>? taskService = null,
		string tenantId = "tenant-a",
		string subject = "user-a",
		params string[] roles)
	{
		var controller = new PersonalTaskController(personal.Object, (taskService ?? new Mock<ITaskService>()).Object);
		var identity = new ClaimsIdentity(
			[new Claim(ClaimTypes.NameIdentifier, subject), new Claim("sub", subject),
			 new Claim("tenant_id", tenantId), .. roles.Select(r => new Claim(ClaimTypes.Role, r))],
			"Test");
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
		};
		return controller;
	}

	private static TaskInboxRow Row()
		=> new(TaskId, "Approve", UserTaskStatus.Pending, Guid.NewGuid(), "tenant-a", DateTime.UtcNow, null, null, null);

	[Fact]
	public async Task Query_ReturnsMinimizedDtos_BoundedByClaimTenantAndCaller()
	{
		var personal = new Mock<IPersonalTaskService>();
		personal.Setup(p => p.QueryInboxAsync(It.IsAny<TaskInboxQuery>(), TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult(new PagedTaskInbox(
				new[] { Row() }, Total: 1, Page: 1, PageSize: 20)));
		var controller = Controller(personal);

		var action = await controller.Query(search: null, page: 1, pageSize: 20, TestContext.Current.CancellationToken);

		var page = Assert.IsType<PagedResultDto<PersonalTaskDto>>(action.Value);
		var dto = Assert.Single(page.Items);
		Assert.IsNotType<UserTask>(dto);
		Assert.Equal(TaskId, dto.Id);
		Assert.Equal("Approve", dto.Name);
		Assert.Equal("pending", dto.Status);
		Assert.True(PersonalTaskStatus.IsControlled(dto.Status));

		personal.Verify(p => p.QueryInboxAsync(
			It.Is<TaskInboxQuery>(q => q.TenantId == "tenant-a" && q.UserId == "user-a"),
			TestContext.Current.CancellationToken), Times.Once);
	}

	[Fact]
	public async Task Detail_ForVisibleTask_ReturnsDto()
	{
		var personal = new Mock<IPersonalTaskService>();
		personal.Setup(p => p.GetVisibleDetailAsync(TaskId, "tenant-a", "user-a", It.IsAny<IReadOnlyCollection<string>>(), TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult<TaskInboxRow?>(Row()));
		var controller = Controller(personal);

		var action = await controller.Detail(TaskId, TestContext.Current.CancellationToken);

		var dto = Assert.IsType<PersonalTaskDto>(action.Value);
		Assert.Equal(TaskId, dto.Id);
	}

	[Fact]
	public async Task Detail_ForUnauthorizedTask_ReturnsNotFound()
	{
		var personal = new Mock<IPersonalTaskService>();
		personal.Setup(p => p.GetVisibleDetailAsync(It.IsAny<Guid>(), "tenant-a", "user-a", It.IsAny<IReadOnlyCollection<string>>(), TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult<TaskInboxRow?>(null));
		var controller = Controller(personal);

		var action = await controller.Detail(TaskId, TestContext.Current.CancellationToken);

		Assert.IsType<NotFoundResult>(action.Result);
	}

	[Fact]
	public async Task Claim_DerivesAssigneeFromClaims_AndReturnsNoContent()
	{
		var personal = new Mock<IPersonalTaskService>();
		personal.Setup(p => p.ClaimForSelfAsync(TaskId, "tenant-a", "user-a", It.IsAny<IReadOnlyCollection<string>>(), TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult(new PersonalClaimResult(PersonalClaimOutcome.Claimed)));
		var controller = Controller(personal);

		// Claim takes NO assignee in the request; the caller identity is the assignee.
		var result = await controller.Claim(TaskId, TestContext.Current.CancellationToken);

		Assert.IsType<NoContentResult>(result);
		personal.Verify(p => p.ClaimForSelfAsync(TaskId, "tenant-a", "user-a", It.IsAny<IReadOnlyCollection<string>>(), TestContext.Current.CancellationToken), Times.Once);
	}

	[Fact]
	public async Task Claim_NotCandidate_ReturnsForbid()
	{
		var personal = new Mock<IPersonalTaskService>();
		personal.Setup(p => p.ClaimForSelfAsync(It.IsAny<Guid>(), "tenant-a", "user-a", It.IsAny<IReadOnlyCollection<string>>(), TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult(new PersonalClaimResult(PersonalClaimOutcome.NotCandidate)));
		var controller = Controller(personal);

		var result = await controller.Claim(TaskId, TestContext.Current.CancellationToken);

		Assert.IsType<ForbidResult>(result);
	}

	[Fact]
	public async Task Claim_StaleRevision_ReturnsConflict()
	{
		var personal = new Mock<IPersonalTaskService>();
		personal.Setup(p => p.ClaimForSelfAsync(It.IsAny<Guid>(), "tenant-a", "user-a", It.IsAny<IReadOnlyCollection<string>>(), TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult(new PersonalClaimResult(PersonalClaimOutcome.StaleRevision)));
		var controller = Controller(personal);

		var result = await controller.Claim(TaskId, TestContext.Current.CancellationToken);

		Assert.IsType<ConflictObjectResult>(result);
	}

	[Fact]
	public async Task Complete_DelegatesToValidatedIdempotentCompletion()
	{
		var personal = new Mock<IPersonalTaskService>();
		var taskService = new Mock<ITaskService>();
		var completion = taskService.As<ITaskCompletionService>();
		taskService.Setup(s => s.GetByIdAsync(TaskId, TestContext.Current.CancellationToken))
			.Returns(() => ValueTask.FromResult<UserTask?>(new UserTask
			{
				Id = TaskId,
				TenantId = "tenant-a",
				Name = "Approve",
				ActivityId = "approve",
				Type = "userTask",
				Assignee = "user-a",
				Status = UserTaskStatus.Pending,
				Revision = 4,
			}));
		var controller = Controller(personal, taskService);

		controller.IdempotencyKey = "attempt-1";
		var request = new PersonalTaskController.CompleteRequest(new Dictionary<string, object>(StringComparer.Ordinal), 4, "hash-1");
		var result = await controller.Complete(TaskId, request, TestContext.Current.CancellationToken);

		Assert.IsType<NoContentResult>(result);
		completion.Verify(s => s.CompleteValidatedAsync(TaskId, It.IsAny<IDictionary<string, object>>(), "attempt-1",
			4, "hash-1", "user-a", "tenant-a", false, It.IsAny<IReadOnlyCollection<string>>(), TestContext.Current.CancellationToken), Times.Once);
	}
}
