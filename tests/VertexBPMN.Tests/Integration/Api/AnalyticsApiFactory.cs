using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Tests.Infrastructure;
using Assert = Xunit.Assert;

namespace VertexBPMN.Tests.Integration.Api;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public xUnit collection or fixture contract retained for framework activation and injection into public test constructors.")]
public sealed class AnalyticsApiFactory : CustomWebApplicationFactory
{

	public async Task SeedTenantIsolationEventsAsync()
	{
		_ = CreateClient();

		using var scope = Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ProcessMiningEventDbContext>();
		if (await db.Events.AnyAsync(e => e.ProcessInstanceId == "tenant-isolation-a"))
		{
			return;
		}

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

}
