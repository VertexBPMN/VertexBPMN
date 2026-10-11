using System.Net;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Studio;

public sealed class ActiveEngineConcurrencyTests
{
	[Fact]
	public async Task ConcurrentRefresh_IsSkipped_AndCancellationReleasesTheGate()
	{
		using var handler = new BlockingReadinessHandler();
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(value => value.CreateClient("VertexBPMN.Api"))
			.Returns(() => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test") });
		using var service = new ActiveEngineService(factory.Object);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		var first = service.CheckConnectionAsync(cancellation.Token);
		await handler.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
		await service.CheckConnectionAsync(TestContext.Current.CancellationToken);
		Assert.Equal(1, handler.RequestCount);

		await cancellation.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
		Assert.False(service.IsChecking);

		handler.AllowCompletion = true;
		await service.CheckConnectionAsync(TestContext.Current.CancellationToken);
		Assert.Equal(2, handler.RequestCount);
		Assert.True(service.IsConnected);
	}

	[Fact]
	public async Task DisposalDuringRefresh_DoesNotAccessDisposedSynchronizationResources()
	{
		using var handler = new BlockingReadinessHandler();
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(value => value.CreateClient("VertexBPMN.Api"))
			.Returns(() => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test") });
		var service = new ActiveEngineService(factory.Object);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		var pending = service.CheckConnectionAsync(cancellation.Token);
		await handler.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

		service.Dispose();
		await cancellation.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
		await Assert.ThrowsAsync<ObjectDisposedException>(() => service.CheckConnectionAsync(TestContext.Current.CancellationToken));
	}

	private sealed class BlockingReadinessHandler : HttpMessageHandler
	{
		public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public bool AllowCompletion { get; set; }
		public int RequestCount { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			RequestCount++;
			Started.TrySetResult();
			if (!AllowCompletion)
			{
				await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
			}
			return new HttpResponseMessage(HttpStatusCode.OK);
		}
	}
}
