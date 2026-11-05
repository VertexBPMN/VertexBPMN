using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Application.Lc06;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Seeding;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

/// <summary>
/// Exercises the real API surfaced released data-source read at the real boundary:
/// `GET /api/applications/{id}/released/data-sources/{bindingName}`. The released
/// revision is created through the real release pipeline (ReferenceApplicationSeeder),
/// so this validates the server-resolved released-revision plus the narrow binding gate.
/// </summary>
public sealed class ReleasedDataSourceApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;
	private readonly HttpClient _client;

	public ReleasedDataSourceApiIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
	{
		_factory = factory;
		_client = factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri("http://localhost")
		});
		_client.Timeout = TimeSpan.FromSeconds(30);
		XunitLoggerProvider.Register(factory.Services.GetRequiredService<ILoggerFactory>(), output);
	}

	private static ApplicationAccessContext Access { get; } = new(
		"released-datasource-test",
		new HashSet<string>(new[] { "application.design", "application.publish" }, StringComparer.Ordinal));

	private async Task<string> SeedReleasedReferenceAppAsync()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();

		var seededId = await ReferenceApplicationSeeder.SeedReleasedAsync(db, Mock.Of<IAuditLogService>(), Access);
		if (seededId is not null)
		{
			return seededId;
		}

		var list = await db.ApplicationDefinitions
			.Where(a => a.TenantId == Lc06ReferenceApp.TenantId && a.Key == Lc06ReferenceApp.Key)
			.ToListAsync();
		return list.Single().Id;
	}

	private static string ReleasedDataSourceUrl(string appId, string bindingName)
		=> $"/api/applications/{appId}/released/data-sources/{bindingName}?tenantId={Lc06ReferenceApp.TenantId}";

	[Fact]
	public async Task Sanctioned_Binding_IsServed_WithTypedRead()
	{
		var appId = await SeedReleasedReferenceAppAsync();

		var response = await _client.GetAsync(ReleasedDataSourceUrl(appId, Lc06ReferenceApp.SanctionedBinding), TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal(Lc06ReferenceApp.SanctionedBinding, body.GetProperty("binding").GetString());
		Assert.Equal(2, body.GetProperty("bindingVersion").GetInt32());
		Assert.Equal("success", body.GetProperty("status").GetString());
		Assert.Equal(0, body.GetProperty("rowCount").GetInt32());
	}

	[Fact]
	public async Task DeclaredButUnreferenced_Binding_IsRejectedWithUnsanctionedError()
	{
		var appId = await SeedReleasedReferenceAppAsync();

		var response = await _client.GetAsync(ReleasedDataSourceUrl(appId, Lc06ReferenceApp.UnreferencedBinding), TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

		var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal("apiUnsanctionedBinding", body.GetProperty("value").GetProperty("code").GetString());
	}

	[Fact]
	public async Task Arbitrary_Binding_IsRejected()
	{
		var appId = await SeedReleasedReferenceAppAsync();

		var response = await _client.GetAsync(ReleasedDataSourceUrl(appId, Lc06ReferenceApp.ArbitraryBinding), TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task UnknownApplication_ReturnsNotFound()
	{
		var response = await _client.GetAsync(ReleasedDataSourceUrl(Guid.NewGuid().ToString("N"), Lc06ReferenceApp.SanctionedBinding), TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}
}
