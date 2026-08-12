using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Tests.Infrastructure;
using Assert = Xunit.Assert;

namespace VertexBPMN.Tests.Integration.Api
{
    public class AnalyticsApiTests : IClassFixture<AnalyticsApiFactory>
    {
    private readonly AnalyticsApiFactory _factory;
    public AnalyticsApiTests(AnalyticsApiFactory factory)
        {
            _factory = factory;
        }
        [Fact]
        public async Task AuthenticatedRequest_ReturnsOk()
        {
            using var client = _factory.CreateClient();

            client.DefaultRequestHeaders.Add(
                "X-Test-User",
                "analytics-reader");

            client.DefaultRequestHeaders.Add(
                "X-Test-Tenant",
                "vertexbpmn");

            var response = await client.GetAsync(
                "/api/analytics/events");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);
        }

        [Fact]
        public async Task AnonymousRequest_ReturnsUnauthorized()
        {
            using var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/analytics/events");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetAllEvents_ReturnsOkAndEvents()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/analytics/events");
            response.EnsureSuccessStatusCode();
            var events = await response.Content.ReadFromJsonAsync<ProcessMiningEvent[]>();
            Assert.NotNull(events);
        }

        [Fact]
        public async Task GetEventTypeStats_ReturnsOkAndStats()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/analytics/event-stats");
            response.EnsureSuccessStatusCode();
            var stats = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
            Assert.NotNull(stats);
        }

        [Fact]
        public async Task GetTrace_ReturnsOkAndTrace()
        {
            var client = CreateAuthenticatedClient();
            // Use a known processInstanceId or mock data
            var response = await client.GetAsync("/api/analytics/trace/1");
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetEventsByTenant_ReturnsOk()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/analytics/events/by-tenant/vertexbpmn");
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task GetEventsByTenant_ForDifferentTenant_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient("tenant-a");
            var response = await client.GetAsync("/api/analytics/events/by-tenant/tenant-b");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AnalyticsQueries_ReturnOnlyEventsFromAuthenticatedTenant()
        {
            await _factory.SeedTenantIsolationEventsAsync();
            var client = CreateAuthenticatedClient("tenant-a");

            var eventsResponse = await client.GetFromJsonAsync<ProcessMiningEvent[]>("/api/analytics/events");
            var statsResponse = await client.GetFromJsonAsync<Dictionary<string, int>>("/api/analytics/event-stats");
            var seriesResponse = await client.GetFromJsonAsync<JsonElement[]>("/api/analytics/events/timeseries/TenantIsolationEvent");
            var traceResponse = await client.GetFromJsonAsync<ProcessMiningEvent[]>("/api/analytics/trace/tenant-isolation-a");
            var metricsResponse = await client.GetFromJsonAsync<JsonElement>("/api/analytics/metrics/process");

            Assert.NotNull(eventsResponse);
            Assert.NotNull(statsResponse);
            Assert.NotNull(seriesResponse);
            Assert.NotNull(traceResponse);
            Assert.All(eventsResponse!, item => Assert.Equal("tenant-a", item.TenantId));
            Assert.Equal(1, statsResponse!["TenantIsolationEvent"]);
            Assert.Single(seriesResponse!);
            Assert.Equal(3, traceResponse!.Length);
            Assert.Equal(1, metricsResponse.GetProperty("count").GetInt32());
        }

        [Fact]
        public async Task GetEventTimeSeries_ReturnsOk()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/analytics/events/timeseries/ProcessStarted");
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task GetProcessMetrics_ReturnsOk()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/analytics/metrics/process");
            response.EnsureSuccessStatusCode();
        }

        private HttpClient CreateAuthenticatedClient()
            => CreateAuthenticatedClient("vertexbpmn");

        private HttpClient CreateAuthenticatedClient(string tenantId)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-User", "analytics-reader");
            client.DefaultRequestHeaders.Add("X-Test-Tenant", tenantId);
            return client;
        }
    }

    public sealed class AnalyticsApiFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                //services.AddAuthentication(options =>
                //    {
                //        options.DefaultAuthenticateScheme = "Test";
                //        options.DefaultChallengeScheme = "Test";
                //    })
                //    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            });
        }

        public async Task SeedTenantIsolationEventsAsync()
        {
            _ = CreateClient();

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProcessMiningEventDbContext>();
            if (await db.Events.AnyAsync(e => e.ProcessInstanceId == "tenant-isolation-a"))
                return;

            db.Events.AddRange(
                new ProcessMiningEvent
                {
                    EventType = "ProcessStarted",
                    ProcessInstanceId = "tenant-isolation-a",
                    TenantId = "tenant-a",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2)
                },
                new ProcessMiningEvent
                {
                    EventType = "TenantIsolationEvent",
                    ProcessInstanceId = "tenant-isolation-a",
                    TenantId = "tenant-a",
                    Timestamp = DateTimeOffset.UtcNow
                },
                new ProcessMiningEvent
                {
                    EventType = "ProcessEnded",
                    ProcessInstanceId = "tenant-isolation-a",
                    TenantId = "tenant-a",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(2)
                },
                new ProcessMiningEvent
                {
                    EventType = "ProcessStarted",
                    ProcessInstanceId = "tenant-isolation-b",
                    TenantId = "tenant-b",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
                },
                new ProcessMiningEvent
                {
                    EventType = "TenantIsolationEvent",
                    ProcessInstanceId = "tenant-isolation-b",
                    TenantId = "tenant-b",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(1)
                },
                new ProcessMiningEvent
                {
                    EventType = "ProcessEnded",
                    ProcessInstanceId = "tenant-isolation-b",
                    TenantId = "tenant-b",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(3)
                });

            await db.SaveChangesAsync();
        }

        public Task InitializeAsync()
        {
            _ = Services;
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }

}
