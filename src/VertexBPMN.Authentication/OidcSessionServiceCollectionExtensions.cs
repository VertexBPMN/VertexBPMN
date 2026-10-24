using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Authentication;

/// <summary>Server-side OIDC session services; hosts retain their protocol and cookie configuration.</summary>
public static class OidcSessionServiceCollectionExtensions
{
	/// <summary>Registers local in-process sessions. This is not a production replica store.</summary>
	public static IServiceCollection AddVertexBpmnOidcSessions(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.AddLogging();
		services.AddOptions();
		services.TryAddSingleton(TimeProvider.System);
		services.TryAddSingleton<ISharedOidcSessionStore, InMemorySharedOidcSessionStore>();
		services.TryAddSingleton<OidcSessionTokenStore>();
		services.TryAddScoped<OidcCookieRefreshEvents>();
		return services;
	}

	/// <summary>
	/// Registers encrypted relational sessions. Configure shared Data Protection keys and a
	/// host-specific application name separately; schema migration remains an explicit host operation.
	/// </summary>
	public static IServiceCollection AddVertexBpmnPersistentOidcSessions(this IServiceCollection services, Action<DbContextOptionsBuilder> configureDatabase)
	{
		ArgumentNullException.ThrowIfNull(configureDatabase);
		services.AddVertexBpmnOidcSessions();
		services.AddDataProtection();
		services.AddDbContextFactory<OidcSessionStoreDbContext>(configureDatabase);
		services.Replace(ServiceDescriptor.Singleton<ISharedOidcSessionStore, PersistentOidcSessionStore>());
		return services;
	}
}
