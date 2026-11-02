using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class OfflineControllerTests
{
	private static readonly Guid TaskA = Guid.NewGuid();
	private static readonly Guid TaskB = Guid.NewGuid();

	private static Dictionary<string, object> Vars() => new(StringComparer.Ordinal) { ["approved"] = true };

	private static OfflineController Controller(
		Mock<IOfflineCompletionReplayService> replay,
		string tenantId = "tenant-a",
		string subject = "user-a",
		params string[] roles)
	{
		var controller = new OfflineController(replay.Object);
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

	[Fact]
	public async Task Replay_ReturnsPerItemResults_ResolvingIdentityFromClaims()
	{
		var replay = new Mock<IOfflineCompletionReplayService>();
		replay.Setup(r => r.ReplayAsync(It.IsAny<IReadOnlyList<OfflineCompletionCommand>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new OfflineCompletionReplayResult[]
			{
				new(TaskA, OfflineCompletionStatus.Completed),
				new(TaskB, OfflineCompletionStatus.Conflict, "staleTaskSnapshot", Revision: 3, FormSchemaHash: "frozen")
			});
		var controller = Controller(replay);

		var action = await controller.Replay(new OfflineController.OfflineReplayRequest(
			[new OfflineCompletionCommand(TaskA, Vars(), "k-1", null, null),
			 new OfflineCompletionCommand(TaskB, null, "k-2", 2, null)],
			TenantId: null), TestContext.Current.CancellationToken);

		var result = Assert.IsType<OkObjectResult>(action);
		var body = Assert.IsType<OfflineController.OfflineReplayResponse>(result.Value);
		Assert.Equal(2, body.Results.Count);
		Assert.Equal(TaskA, body.Results[0].TaskId);
		Assert.Equal(OfflineCompletionStatus.Completed, body.Results[0].Status);
		Assert.Equal(OfflineCompletionStatus.Conflict, body.Results[1].Status);
		Assert.Equal("staleTaskSnapshot", body.Results[1].ConflictCode);

		replay.Verify(r => r.ReplayAsync(
			It.Is<IReadOnlyList<OfflineCompletionCommand>>(l => l.Count == 2),
			"user-a", "tenant-a", false, It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()));
	}

	[Fact]
	public async Task Admin_UsesExplicitRequestTenant()
	{
		var replay = new Mock<IOfflineCompletionReplayService>();
		replay.Setup(r => r.ReplayAsync(It.IsAny<IReadOnlyList<OfflineCompletionCommand>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Array.Empty<OfflineCompletionReplayResult>());
		var controller = Controller(replay, tenantId: "tenant-a", roles: "Admin");

		await controller.Replay(new OfflineController.OfflineReplayRequest(Array.Empty<OfflineCompletionCommand>(), TenantId: "tenant-b"), TestContext.Current.CancellationToken);

		replay.Verify(r => r.ReplayAsync(It.IsAny<IReadOnlyList<OfflineCompletionCommand>>(), "user-a", "tenant-b", true, It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()));
	}

	[Fact]
	public async Task NonAdmin_ResolvesTenantFromClaim_IgnoringRequestTenant()
	{
		var replay = new Mock<IOfflineCompletionReplayService>();
		replay.Setup(r => r.ReplayAsync(It.IsAny<IReadOnlyList<OfflineCompletionCommand>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Array.Empty<OfflineCompletionReplayResult>());
		var controller = Controller(replay, tenantId: "tenant-a");

		await controller.Replay(new OfflineController.OfflineReplayRequest(Array.Empty<OfflineCompletionCommand>(), TenantId: "tenant-b"), TestContext.Current.CancellationToken);

		replay.Verify(r => r.ReplayAsync(It.IsAny<IReadOnlyList<OfflineCompletionCommand>>(), "user-a", "tenant-a", false, It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()));
	}
}
