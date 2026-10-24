using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Authentication.Tests;

public sealed class RegistrationAndMigrationTests
{
	[Fact]
	public async Task PersistentRegistration_ResolvesSharedServicesAndMigratesWithoutModelDrift()
	{
		var services = new ServiceCollection();
		services.AddVertexBpmnPersistentOidcSessions(options => options.UseSqlite("Data Source=:memory:"));
		await using var provider = services.BuildServiceProvider();
		Assert.IsType<PersistentOidcSessionStore>(provider.GetRequiredService<ISharedOidcSessionStore>());
		Assert.NotNull(provider.GetRequiredService<OidcSessionTokenStore>());
		await using var scope = provider.CreateAsyncScope();
		Assert.NotNull(scope.ServiceProvider.GetRequiredService<OidcCookieRefreshEvents>());
		await using var db = provider.GetRequiredService<IDbContextFactory<OidcSessionStoreDbContext>>().CreateDbContext();
		await db.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
		await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
		Assert.False(db.Database.HasPendingModelChanges());
		Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task LocalRegistration_RemainsInMemory()
	{
		await using var provider = new ServiceCollection().AddVertexBpmnOidcSessions().BuildServiceProvider();
		Assert.IsType<InMemorySharedOidcSessionStore>(provider.GetRequiredService<ISharedOidcSessionStore>());
		Assert.NotNull(provider.GetRequiredService<OidcSessionTokenStore>());
	}
}
