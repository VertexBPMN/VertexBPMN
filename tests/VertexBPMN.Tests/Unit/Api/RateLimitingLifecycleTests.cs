using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Api;
using VertexBPMN.Api.Services;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class RateLimitingLifecycleTests
{
	[Fact]
	public void ConstructionDoesNotStartAnUnownedCleanupTask()
	{
		using var service = new ProductionRateLimitingService(NullLogger<ProductionRateLimitingService>.Instance);
		Assert.Null(service.ExecuteTask);
	}

	[Fact]
	public async Task HostLifetimeStartsAndStopsCleanupWithoutWaitingForItsTimer()
	{
		using var service = new ProductionRateLimitingService(NullLogger<ProductionRateLimitingService>.Instance);
		await service.StartAsync(TestContext.Current.CancellationToken);
		Assert.NotNull(service.ExecuteTask);

		await service.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

		Assert.True(service.ExecuteTask.IsCompleted);
	}

	[Fact]
	public async Task RequestsAcrossScopesShareTheSameBudgetAndHostedInstance()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddApiServices(new ConfigurationBuilder().Build());
		await using var provider = services.BuildServiceProvider();
		using var firstScope = provider.CreateScope();
		using var secondScope = provider.CreateScope();
		var first = firstScope.ServiceProvider.GetRequiredService<IRateLimitingService>();
		var second = secondScope.ServiceProvider.GetRequiredService<IRateLimitingService>();

		Assert.Same(first, second);
		Assert.Same(first, Assert.Single(provider.GetServices<IHostedService>()));
		for (var request = 0; request < 100; request++)
		{
			Assert.True((request % 2 == 0 ? first : second).IsAllowed("test-client", "default"));
		}

		Assert.False(second.IsAllowed("test-client", "default"));
		Assert.Equal(0, first.GetRateLimitInfo("test-client", "default").Remaining);
	}

	[Theory]
	[InlineData("X-Forwarded-For")]
	[InlineData("X-Real-IP")]
	public async Task RawProxyHeadersCannotSelectAnotherClientsBudget(string headerName)
	{
		var limiter = new Mock<IRateLimitingService>(MockBehavior.Strict);
		limiter.Setup(service => service.IsAllowed("ip:192.0.2.10", "default")).Returns(true);
		limiter.Setup(service => service.GetRateLimitInfo("ip:192.0.2.10", "default")).Returns(new RateLimitInfo
		{
			Limit = 100,
			Remaining = 99,
			ResetTime = DateTime.UtcNow.AddMinutes(1)
		});
		var services = new ServiceCollection();
		services.AddSingleton(limiter.Object);
		await using var provider = services.BuildServiceProvider();
		var httpContext = new DefaultHttpContext { RequestServices = provider };
		httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
		httpContext.Request.Headers[headerName] = "198.51.100.99";
		var action = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
		var controller = new object();
		var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(StringComparer.Ordinal), controller);
		var nextInvoked = false;

		await new RateLimitAttribute().OnActionExecutionAsync(context, () =>
		{
			nextInvoked = true;
			return Task.FromResult(new ActionExecutedContext(action, [], controller));
		});

		Assert.True(nextInvoked);
		limiter.VerifyAll();
	}
}
