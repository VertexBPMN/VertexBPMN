using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Authentication.Tests;

public sealed class CrossInstanceSessionTests
{
	[Fact]
	public async Task LogoutLoadedBeforeRotation_RetriesUntilRevoked()
	{
		await using var fixture = await SessionFixture.CreateAsync();
		await fixture.First.PutAsync("session-a", fixture.Principal, "access", "refresh", null, DateTimeOffset.UtcNow.AddMinutes(5), 0, TestContext.Current.CancellationToken);
		var fence = new PauseSaveInterceptor();
		var logout = fixture.StoreWithInterceptor(fence).RemoveAsync("session-a", TestContext.Current.CancellationToken);
		await fence.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
		await fixture.Second.PutAsync("session-a", fixture.Principal, "rotated-access", "rotated-refresh", null, DateTimeOffset.UtcNow.AddMinutes(5), 0, TestContext.Current.CancellationToken);
		fence.Resume.SetResult();
		await logout;
		Assert.Null(await fixture.Second.TryGetAsync("session-a", TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task DelayedRefreshWhileLocalCacheHydratesWinner_DoesNotOverwriteRotation()
	{
		await using var fixture = await SessionFixture.CreateAsync();
		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var first = fixture.TokenStore(async () =>
		{
			started.SetResult();
			await resume.Task.WaitAsync(TestContext.Current.CancellationToken);
			return fixture.Response();
		}, fixture.First);
		using var second = fixture.TokenStore(() => Task.FromResult(fixture.Response()), fixture.Second);
		first.Store.Register(fixture.Principal, SessionFixture.Properties());
		var delayed = first.Store.GetAccessTokenAsync(fixture.Principal, null, TestContext.Current.CancellationToken);
		await started.Task.WaitAsync(TestContext.Current.CancellationToken);
		var winner = await second.Store.GetAccessTokenAsync(fixture.Principal, null, TestContext.Current.CancellationToken);
		Assert.Equal(winner, await first.Store.GetAccessTokenAsync(fixture.Principal, null, TestContext.Current.CancellationToken));
		resume.SetResult();
		Assert.Equal(winner, await delayed);
		Assert.Equal(1, (await fixture.Second.TryGetAsync("session-a", TestContext.Current.CancellationToken))?.Revision);
	}

	[Fact]
	public async Task InMemoryRevocation_RejectsDelayedRefreshWrite()
	{
		var store = new InMemorySharedOidcSessionStore();
		var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "oidc"));
		await store.PutAsync("session-a", principal, "access", "refresh", null, DateTimeOffset.UtcNow.AddMinutes(5), 0, TestContext.Current.CancellationToken);
		await store.RemoveAsync("session-a", TestContext.Current.CancellationToken);
		await Assert.ThrowsAsync<OidcSessionRevisionConflictException>(() => store.PutAsync("session-a", principal, "late-access", "late-refresh", null, DateTimeOffset.UtcNow.AddMinutes(5), 0, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task WriterLoadedBeforeLogout_IsFencedAtDatabaseCommit()
	{
		await using var fixture = await SessionFixture.CreateAsync();
		await fixture.First.PutAsync("session-a", fixture.Principal, "access", "refresh", null, DateTimeOffset.UtcNow.AddMinutes(5), 0, TestContext.Current.CancellationToken);
		var fence = new PauseSaveInterceptor();
		var writer = fixture.StoreWithInterceptor(fence);
		var delayed = writer.PutAsync("session-a", fixture.Principal, "late-access", "late-refresh", null, DateTimeOffset.UtcNow.AddMinutes(5), 0, TestContext.Current.CancellationToken);
		await fence.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
		await fixture.Second.RemoveAsync("session-a", TestContext.Current.CancellationToken);
		fence.Resume.SetResult();
		await Assert.ThrowsAsync<OidcSessionRevisionConflictException>(() => delayed);
		Assert.Null(await fixture.Second.TryGetAsync("session-a", TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task StaleRefreshRejectedAfterWinnerRotation_DoesNotRevokeWinner()
	{
		await using var fixture = await SessionFixture.CreateAsync();
		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var first = fixture.TokenStore(async () =>
		{
			started.SetResult();
			await resume.Task.WaitAsync(TestContext.Current.CancellationToken);
			return new HttpResponseMessage(HttpStatusCode.BadRequest);
		}, fixture.First);
		using var second = fixture.TokenStore(() => Task.FromResult(fixture.Response()), fixture.Second);
		first.Store.Register(fixture.Principal, SessionFixture.Properties());
		var stale = first.Store.GetAccessTokenAsync(fixture.Principal, null, TestContext.Current.CancellationToken);
		await started.Task.WaitAsync(TestContext.Current.CancellationToken);
		var winner = await second.Store.GetAccessTokenAsync(fixture.Principal, null, TestContext.Current.CancellationToken);
		resume.SetResult();
		Assert.Equal(winner, await stale);
		Assert.Equal(winner, (await fixture.Second.TryGetAsync("session-a", TestContext.Current.CancellationToken))?.AccessToken);
	}

	[Fact]
	public async Task LoggedOutReplica_RejectsCachedUnexpiredAccessToken()
	{
		await using var fixture = await SessionFixture.CreateAsync();
		using var first = fixture.TokenStore(() => Task.FromResult(fixture.Response()), fixture.First);
		using var second = fixture.TokenStore(() => Task.FromResult(fixture.Response()), fixture.Second);
		first.Store.Register(fixture.Principal, SessionFixture.Properties());
		await first.Store.GetAccessTokenAsync(fixture.Principal, null, TestContext.Current.CancellationToken);
		second.Store.Remove(fixture.Principal);
		await Assert.ThrowsAsync<OidcSessionExpiredException>(() => first.Store.GetAccessTokenAsync(fixture.Principal, SessionFixture.Properties(), TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task ReplicaWithStaleTicket_UsesSharedRotationWithoutRefreshingOldToken()
	{
		await using var fixture = await SessionFixture.CreateAsync();
		using var first = fixture.TokenStore(() => Task.FromResult(fixture.Response()), fixture.First);
		var calls = 0;
		using var second = fixture.TokenStore(() =>
		{
			calls++;
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
		}, fixture.Second);
		first.Store.Register(fixture.Principal, SessionFixture.Properties());
		var winner = await first.Store.GetAccessTokenAsync(fixture.Principal, null, TestContext.Current.CancellationToken);
		var replica = await second.Store.GetAccessTokenAsync(fixture.Principal, SessionFixture.Properties(), TestContext.Current.CancellationToken);
		Assert.Equal(winner, replica);
		Assert.Equal(0, calls);
		Assert.Equal("ProcessManager", second.Store.GetCurrentPrincipal(fixture.Principal).FindFirstValue(ClaimTypes.Role));
	}

	[Fact]
	public async Task DelayedRefreshAfterOtherReplicaLogout_NeverResurrectsSession()
	{
		await using var fixture = await SessionFixture.CreateAsync();
		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var first = fixture.TokenStore(async () =>
		{
			started.SetResult();
			await resume.Task.WaitAsync(TestContext.Current.CancellationToken);
			return fixture.Response();
		}, fixture.First);
		using var second = fixture.TokenStore(() => Task.FromResult(fixture.Response()), fixture.Second);
		first.Store.Register(fixture.Principal, SessionFixture.Properties());
		var refresh = first.Store.GetAccessTokenAsync(fixture.Principal, null, TestContext.Current.CancellationToken);
		await started.Task.WaitAsync(TestContext.Current.CancellationToken);
		second.Store.Remove(fixture.Principal);
		resume.SetResult();
		await Assert.ThrowsAsync<OidcSessionExpiredException>(() => refresh);
		Assert.Null(await fixture.Second.TryGetAsync("session-a", TestContext.Current.CancellationToken));
	}

	private sealed class SessionFixture : IAsyncDisposable
	{
		private const string Issuer = "https://issuer.vertexbpmn.test";
		private readonly string _directory = Path.Combine(Environment.GetEnvironmentVariable("TMPDIR") ?? throw new InvalidOperationException("TMPDIR required"), "lc06-" + Guid.NewGuid().ToString("N"));
		private readonly RSA _key = RSA.Create(2048);
		private SessionFixture() { }
		public PersistentOidcSessionStore First { get; private set; } = null!;
		public PersistentOidcSessionStore Second { get; private set; } = null!;
		public ClaimsPrincipal Principal { get; } = new(new ClaimsIdentity([
			new Claim("sub", "user-1"), new Claim("tenant_id", "tenant-a"),
			new Claim(OidcSessionTokenStore.SessionIdClaim, "session-a")], "oidc"));
		public static async Task<SessionFixture> CreateAsync()
		{
			var fixture = new SessionFixture();
			Directory.CreateDirectory(fixture._directory);
			var factory = new DbFactory(new DbContextOptionsBuilder<OidcSessionStoreDbContext>().UseSqlite("Data Source=" + Path.Combine(fixture._directory, "sessions.db")).Options);
			await using var db = factory.CreateDbContext();
			await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
			var keys = new DirectoryInfo(Path.Combine(fixture._directory, "keys"));
			fixture.First = new PersistentOidcSessionStore(factory, DataProtectionProvider.Create(keys, b => b.SetApplicationName("LC06.Tests")));
			fixture.Second = new PersistentOidcSessionStore(factory, DataProtectionProvider.Create(keys, b => b.SetApplicationName("LC06.Tests")));
			return fixture;
		}
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "TokenContext owns the HttpClient, which owns its RefreshHandler; every caller disposes TokenContext.")]
		public TokenContext TokenStore(Func<Task<HttpResponseMessage>> response, ISharedOidcSessionStore store)
		{
			var configuration = new OpenIdConnectConfiguration { Issuer = Issuer, TokenEndpoint = Issuer + "/token" };
			configuration.SigningKeys.Add(new RsaSecurityKey(_key) { KeyId = "key" });
			var client = new HttpClient(new RefreshHandler(response));
			var options = new OpenIdConnectOptions { ClientId = "studio", ClientSecret = "test-only", Configuration = configuration, Backchannel = client, MapInboundClaims = false, TokenHandler = new JsonWebTokenHandler() };
			return new TokenContext(new OidcSessionTokenStore(new OptionsMonitor(options), TimeProvider.System, NullLogger<OidcSessionTokenStore>.Instance, store), client);
		}
		public HttpResponseMessage Response()
		{
			var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
			{
				Issuer = Issuer, Audience = "vertexbpmn-api", Expires = DateTime.UtcNow.AddMinutes(5),
				Claims = new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = "user-1", ["tenant_id"] = "tenant-a", ["roles"] = "ProcessManager" },
				SigningCredentials = new SigningCredentials(new RsaSecurityKey(_key) { KeyId = "key" }, SecurityAlgorithms.RsaSha256)
			});
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { access_token = token, refresh_token = "rotated-refresh", expires_in = 300 }), Encoding.UTF8, "application/json") };
		}
		public static AuthenticationProperties Properties()
		{
			var properties = new AuthenticationProperties();
			properties.StoreTokens([
				new AuthenticationToken { Name = "access_token", Value = "old-access" },
				new AuthenticationToken { Name = "refresh_token", Value = "old-refresh" },
				new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddSeconds(30).ToString("O", CultureInfo.InvariantCulture) }]);
			return properties;
		}
		public PersistentOidcSessionStore StoreWithInterceptor(Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor interceptor)
		{
			var options = new DbContextOptionsBuilder<OidcSessionStoreDbContext>().UseSqlite("Data Source=" + Path.Combine(_directory, "sessions.db")).AddInterceptors(interceptor).Options;
			return new PersistentOidcSessionStore(new DbFactory(options), DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(_directory, "keys")), b => b.SetApplicationName("LC06.Tests")));
		}
		public ValueTask DisposeAsync()
		{
			_key.Dispose();
			Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
			Directory.Delete(_directory, recursive: true);
			return ValueTask.CompletedTask;
		}
	}
	private sealed class PauseSaveInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
	{
		public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _calls;
		public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default)
		{
			if (Interlocked.Increment(ref _calls) == 1)
			{
				Started.SetResult();
				await Resume.Task.WaitAsync(cancellationToken);
			}
			return result;
		}
	}
	private sealed class DbFactory(DbContextOptions<OidcSessionStoreDbContext> options) : IDbContextFactory<OidcSessionStoreDbContext>
	{
		public OidcSessionStoreDbContext CreateDbContext() => new(options);
	}
	private sealed class RefreshHandler(Func<Task<HttpResponseMessage>> response) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response();
	}
	private sealed class OptionsMonitor(OpenIdConnectOptions options) : IOptionsMonitor<OpenIdConnectOptions>
	{
		public OpenIdConnectOptions CurrentValue => options;
		public OpenIdConnectOptions Get(string? name) => options;
		public IDisposable? OnChange(Action<OpenIdConnectOptions, string?> listener) => null;
	}
	private sealed record TokenContext(OidcSessionTokenStore Store, HttpClient Client) : IDisposable
	{
		public void Dispose() => Client.Dispose();
	}
}
