using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Moq;
using VertexBPMN.Api.Config;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Cmn;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class CmmnRequestCancellationTests
{
	[Theory]
	[InlineData("execute")]
	[InlineData("trigger-event")]
	[InlineData("update-casefile")]
	[InlineData("generate-adhoc")]
	public async Task CmmnEndpointPassesRequestAbortedToEngine(string operation)
	{
		using var requestLifetime = new CancellationTokenSource();
		var requestToken = requestLifetime.Token;
		var engine = new Mock<IProcessEngine>(MockBehavior.Strict);
		var model = new CaseModel("case-1", "Case", [], [], []);
		switch (operation)
		{
			case "execute":
				engine.Setup(service => service.GetCmmnModelAsync("case-1")).ReturnsAsync(model);
				engine.Setup(service => service.ExecuteCaseAsync(model, requestToken)).ReturnsAsync([]);
				break;
			case "trigger-event":
				engine.Setup(service => service.TriggerUserEventAsync("case-1", "event-1", It.IsAny<Dictionary<string, object>>(), requestToken)).Returns(Task.CompletedTask);
				break;
			case "update-casefile":
				engine.Setup(service => service.UpdateCaseFileItemAsync("case-1", "item-1", It.IsAny<object>(), requestToken)).Returns(Task.CompletedTask);
				break;
			case "generate-adhoc":
				engine.Setup(service => service.GenerateAdHocSubprocessAsync("case-1", requestToken)).Returns(Task.CompletedTask);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(operation));
		}

		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		await using var app = builder.Build();
		app.Use((context, next) =>
		{
			context.RequestAborted = requestToken;
			return next(context);
		});
		app.MapVertexBPMNApi(engine.Object);
		await app.StartAsync(TestContext.Current.CancellationToken);
		using var client = app.GetTestClient();
		using var response = await client.PostAsJsonAsync($"/api/cmmn/{operation}", new
		{
			CaseId = "case-1",
			CmmnXml = "<case />",
			EventId = "event-1",
			EventData = new Dictionary<string, object>(StringComparer.Ordinal),
			CaseFileItemId = "item-1",
			NewValue = "updated"
		}, TestContext.Current.CancellationToken);

		response.EnsureSuccessStatusCode();
		engine.VerifyAll();
	}
}
