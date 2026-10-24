using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using VertexBPMN.Authentication;
using VertexBPMN.Portal.Bff;
using VertexBPMN.Studio.Services;

var builder = WebApplication.CreateBuilder(args);

// Portal-BFF: server-side authentication/token holding (LC06). The host owns its
// OIDC/cookie configuration and is inert until an external identity provider is
// configured; development boots token-free so the shell renders without an IdP.
var oidcAuthority = builder.Configuration["Oidc:Authority"];

builder.Services.AddProblemDetails();

builder.Services.AddAntiforgery(options =>
{
    // The BFF reads the request token from a header so JSON actions are CSRF-validated.
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
// AddControllersWithViews registers the ValidateAntiforgeryToken filter that
// [ValidateAntiForgeryToken] needs; plain AddControllers does not pull in the
// ViewFeatures antiforgery filters. No server-side views are used.
builder.Services.AddControllersWithViews();

// Shared server-side OIDC session/token holding and cookie refresh. The BFF retains
// its own relational session store (SQLite locally; Npgsql for replicas) and its own
// Data Protection keys, separate from Studio's.
RegisterOidcSessionStore(builder);

// Released application runtime gateway (server-side API proxy).
builder.Services.AddScoped<IBffRuntimeGateway, VertexBffRuntimeGateway>();

if (!string.IsNullOrWhiteSpace(oidcAuthority))
{
    var oidcClientId = builder.Configuration["Oidc:ClientId"] ?? string.Empty;
    var oidcClientSecret = builder.Configuration["Oidc:ClientSecret"] ?? string.Empty;
    var oidcRequireHttpsMetadata = !string.Equals(builder.Configuration["Oidc:RequireHttpsMetadata"], "false", StringComparison.OrdinalIgnoreCase);

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.Name = "vertexbpmn.portal";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = oidcRequireHttpsMetadata ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            // Server-side token holding: the cookie stores only a session id; the
            // access/refresh tokens live in the encrypted shared session store.
            options.EventsType = typeof(OidcCookieRefreshEvents);
        })
        .AddOpenIdConnect(options =>
        {
            options.Authority = oidcAuthority;
            options.ClientId = oidcClientId;
            options.ClientSecret = oidcClientSecret;
            options.RequireHttpsMetadata = oidcRequireHttpsMetadata;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "preferred_username";
            options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
            options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            options.Events.OnTokenValidated = context =>
            {
                var identity = context.Principal?.Identities.FirstOrDefault(item => item.IsAuthenticated);
                if (identity is null)
                {
                    context.Fail("OIDC did not produce an authenticated identity.");
                    return Task.CompletedTask;
                }

                if (!identity.HasClaim(claim => claim.Type == OidcSessionTokenStore.SessionIdClaim))
                {
                    identity.AddClaim(new Claim(OidcSessionTokenStore.SessionIdClaim, Guid.NewGuid().ToString("N")));
                }

                // Claim normalization runs inside the shared token store's refresh path;
                // the host only guarantees the session identifier for server-side token holding.
                return Task.CompletedTask;
            };
            options.Events.OnTicketReceived = context =>
            {
                var store = context.HttpContext.RequestServices.GetRequiredService<OidcSessionTokenStore>();
                try
                {
                    store.Register(context.Principal!, context.Properties!);
                }
                catch (OidcSessionExpiredException exception)
                {
                    context.Fail(exception);
                }

                return Task.CompletedTask;
            };
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("roles");
            options.Scope.Add("tenant_id");
        });
}

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();

// Blazor-WebAssembly-Client (Portal.Client) als Fallback ausliefern.
app.MapFallbackToFile("index.html");

await app.RunAsync();

static void RegisterOidcSessionStore(WebApplicationBuilder builder)
{
    var connectionString = builder.Configuration.GetConnectionString("OidcSessionStore")
        ?? builder.Configuration["OidcSessionStore:ConnectionString"]
        ?? "Data Source=portal-oidc-sessions.db";
    var provider = OidcSessionStoreProvider.Resolve(connectionString, builder.Configuration["OidcSessionStore:Provider"]);

    if (OidcSessionStoreProvider.IsSqlite(provider) || string.IsNullOrWhiteSpace(connectionString))
    {
        // Shared encrypted relational store on SQLite for single-host operation.
        builder.Services.AddVertexBpmnPersistentOidcSessions(options =>
            OidcSessionStoreProvider.Configure(options, provider, connectionString));
        builder.Services.AddDataProtection().SetApplicationName("VertexBPMN.Portal");
        return;
    }

    // PostgreSQL for multi-host replicas (shared durable sessions + Data Protection keys).
    builder.Services.AddVertexBpmnPersistentOidcSessions(options =>
        OidcSessionStoreProvider.Configure(options, provider, connectionString));
    builder.Services.AddDataProtection().SetApplicationName("VertexBPMN.Portal");
}
