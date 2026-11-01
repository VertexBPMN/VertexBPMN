using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.PersonalTasks;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Tests.Integration.Applications;

public sealed class EndpointOperationLimitTests
{
    [Fact]
    public async Task Post_RejectsRequestBodyOverMaxRequestBytes_WithPayloadTooLarge()
    {
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();
        // Default POST endpoint authors maxRequestBytes = 1024; a 4 KiB idempotency body
        // exceeds it and must be rejected before the request is accepted.
        using var response = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start",
            new { idempotencyKey = new string('x', 4096) },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Get_TimesOutWhenQueryExceedsEndpointTimeout_WithGatewayTimeout()
    {
        await using var fixture = new EndpointRuntimeFixture();
        var content = EndpointRuntimeContent.Json
            .Replace("\"timeoutSeconds\":5", "\"timeoutSeconds\":1", StringComparison.Ordinal);
        // The real inbox query completes instantly; substitute a service that blocks until
        // cancelled so the authorised endpoint timeout (1s) deterministically fires.
        await fixture.OpenAsync(content, configureServices: services =>
            services.AddSingleton<IPersonalTaskService, BlockingTaskService>());
        using var response = await fixture.Http.GetAsync(
            $"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    private sealed class BlockingTaskService : IPersonalTaskService
    {
        public async ValueTask<PagedTaskInbox> QueryInboxAsync(
            TaskInboxQuery query, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new PagedTaskInbox(Array.Empty<TaskInboxRow>(), 0, 1, 20);
        }

        public ValueTask<TaskInboxRow?> GetVisibleDetailAsync(Guid taskId, string tenantId, string userId,
            IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
            => new((TaskInboxRow?)null);

        public ValueTask<PersonalClaimResult> ClaimForSelfAsync(Guid taskId, string tenantId, string userId,
            IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
            => new(new PersonalClaimResult(PersonalClaimOutcome.NotFound));
    }
}
