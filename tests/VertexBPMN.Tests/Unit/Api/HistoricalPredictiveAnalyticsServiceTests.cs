using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Api.ML;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class HistoricalPredictiveAnalyticsServiceTests
{
    [Fact]
    public async Task PredictDuration_UsesCompletedHistoricalInstancesForCurrentTenant()
    {
        await using var db = CreateContext();
        db.Events.AddRange(
            Event("tenant-a", "instance-1", "ProcessStarted", DateTimeOffset.Parse("2026-01-01T08:00:00Z"),
                """{"processDefinitionKey":"invoice","variables":{"amount":100}}"""),
            Event("tenant-a", "instance-1", "ProcessEnded", DateTimeOffset.Parse("2026-01-01T08:10:00Z"),
                """{"processDefinitionKey":"invoice"}"""),
            Event("tenant-a", "instance-2", "ProcessStarted", DateTimeOffset.Parse("2026-01-02T09:00:00Z"),
                """{"processDefinitionKey":"invoice","variables":{"amount":200,"priority":"high"}}"""),
            Event("tenant-a", "instance-2", "ProcessEnded", DateTimeOffset.Parse("2026-01-02T09:30:00Z"),
                """{"processDefinitionKey":"invoice"}"""),
            Event("tenant-b", "instance-3", "ProcessStarted", DateTimeOffset.Parse("2026-01-03T09:00:00Z"),
                """{"processDefinitionKey":"invoice"}"""),
            Event("tenant-b", "instance-3", "ProcessEnded", DateTimeOffset.Parse("2026-01-03T10:30:00Z"),
                """{"processDefinitionKey":"invoice"}"""));
        await db.SaveChangesAsync();

        var service = CreateService(db, "tenant-a");
        var prediction = await service.PredictProcessDurationAsync(
            "invoice",
            new Dictionary<string, object> { ["amount"] = 50 });

        Assert.Equal("invoice", prediction.ProcessDefinitionKey);
        Assert.InRange(prediction.EstimatedDurationMinutes, 0.1f, 60f);
        Assert.InRange(prediction.ConfidenceScore, 0.1f, 0.95f);
        Assert.Contains("2 completed historical instances", prediction.InfluencingFactors);
    }

    [Fact]
    public async Task ExportTrainingData_IsTenantIsolatedAndContainsModelFeatures()
    {
        await using var db = CreateContext();
        db.Events.AddRange(
            Event("tenant-a", "instance-1", "ProcessStarted", DateTimeOffset.Parse("2026-01-01T08:00:00Z"),
                """{"processDefinitionKey":"invoice","variables":{"amount":100}}"""),
            Event("tenant-a", "instance-1", "ProcessEnded", DateTimeOffset.Parse("2026-01-01T08:10:00Z"),
                """{"processDefinitionKey":"invoice"}"""),
            Event("tenant-b", "instance-2", "ProcessStarted", DateTimeOffset.Parse("2026-01-01T08:00:00Z"),
                """{"processDefinitionKey":"invoice"}"""),
            Event("tenant-b", "instance-2", "ProcessEnded", DateTimeOffset.Parse("2026-01-01T08:20:00Z"),
                """{"processDefinitionKey":"invoice"}"""));
        await db.SaveChangesAsync();

        var csv = await CreateService(db, "tenant-a").ExportTrainingDataAsync("invoice");

        Assert.Contains("tenantId,processDefinitionKey,processInstanceId", csv);
        Assert.Contains("\"tenant-a\"", csv);
        Assert.DoesNotContain("\"tenant-b\"", csv);
        Assert.Contains(",10,", csv);
    }

    [Fact]
    public async Task RequestedTenantOutsideClaim_IsRejected()
    {
        await using var db = CreateContext();
        var service = CreateService(db, "tenant-a");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ExportTrainingDataAsync(requestedTenantId: "tenant-b"));
    }

    private static HistoricalPredictiveAnalyticsService CreateService(
        ProcessMiningEventDbContext db,
        string tenantId)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("tenant_id", tenantId)],
                    "Test"))
            }
        };

        return new HistoricalPredictiveAnalyticsService(
            db,
            httpContextAccessor,
            NullLogger<HistoricalPredictiveAnalyticsService>.Instance);
    }

    private static ProcessMiningEvent Event(
        string tenantId,
        string instanceId,
        string eventType,
        DateTimeOffset timestamp,
        string payload) =>
        new()
        {
            EventType = eventType,
            ProcessInstanceId = instanceId,
            TenantId = tenantId,
            Timestamp = timestamp,
            PayloadJson = payload
        };

    private static ProcessMiningEventDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProcessMiningEventDbContext>()
            .UseInMemoryDatabase($"ml-tests-{Guid.NewGuid():N}")
            .Options;
        return new ProcessMiningEventDbContext(options);
    }
}
