using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using VertexBPMN.Core.Services;
using VertexBPMN.Persistence.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using VertexBPMN.Api;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace VertexBPMN.Tests.Api
{
    public class AnalyticsApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        public AnalyticsApiTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetAllEvents_ReturnsOkAndEvents()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/analytics/events");
            response.EnsureSuccessStatusCode();
            var events = await response.Content.ReadFromJsonAsync<ProcessMiningEvent[]>();
            Assert.NotNull(events);
        }

        [Fact]
        public async Task GetEventTypeStats_ReturnsOkAndStats()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/analytics/event-stats");
            response.EnsureSuccessStatusCode();
            var stats = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
            Assert.NotNull(stats);
        }

        [Fact]
        public async Task GetTrace_ReturnsOkAndTrace()
        {
            var client = _factory.CreateClient();
            // Use a known processInstanceId or mock data
            var response = await client.GetAsync("/api/analytics/trace/1");
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetEventsByTenant_ReturnsOk()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/analytics/events/by-tenant/vertexbpmn");
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task GetEventTimeSeries_ReturnsOk()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/analytics/events/timeseries/ProcessStarted");
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task GetProcessMetrics_ReturnsOk()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/analytics/metrics/process");
            response.EnsureSuccessStatusCode();
        }
    }
}
