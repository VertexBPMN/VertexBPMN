using VertexBPMN.Studio.Components;
using VertexBPMN.Studio;
using VertexBPMN.Studio.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;
using System.Net;
using System.Security.Claims;
using VertexBPMN.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);
// Hosting request-start logs include the query string (OAuth2 authorization codes).
// Keep warning/error diagnostics without logging callback query credentials at Information.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.AddServiceDefaults();
var isUiTest = builder.Environment.IsEnvironment("UiTest")
    && string.Equals(
        builder.Configuration["StudioAuthentication:UiTestEnabled"],
        "true",
        StringComparison.OrdinalIgnoreCase);
var isLocalDevelopment = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("StudioAuthentication:LocalDevelopmentEnabled");
var httpsRedirectionEnabled = builder.Configuration.GetValue(
    "StudioHttpsRedirection:Enabled",
    true);
var operationalMode = builder.Configuration["OperationalMode"] ?? builder.Environment.EnvironmentName;
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("VertexBPMN.Studio");
var dataProtectionKeyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
if (operationalMode.Equals("Production", StringComparison.OrdinalIgnoreCase)
    || operationalMode.Equals("Stage", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(dataProtectionKeyRingPath))
    {
        throw new InvalidOperationException(
            "DataProtection:KeyRingPath is required in Production and Stage so Studio replicas share durable authentication keys.");
    }
}
if (!string.IsNullOrWhiteSpace(dataProtectionKeyRingPath))
{
    dataProtection.PersistKeysToFileSystem(
        Directory.CreateDirectory(Path.GetFullPath(dataProtectionKeyRingPath)));
}
var reverseProxyEnabled = builder.Configuration.GetValue<bool>("ReverseProxy:Enabled");
var knownProxyValues = builder.Configuration
    .GetSection("ReverseProxy:KnownProxies")
    .Get<string[]>() ?? [];
var knownProxies = new List<IPAddress>();
foreach (var value in knownProxyValues)
{
    if (!IPAddress.TryParse(value, out var address))
        throw new InvalidOperationException($"ReverseProxy:KnownProxies contains invalid IP address '{value}'.");
    knownProxies.Add(address);
}
if (reverseProxyEnabled && knownProxies.Count == 0)
{
    throw new InvalidOperationException(
        "ReverseProxy:KnownProxies must contain at least one explicit proxy IP when ReverseProxy:Enabled is true.");
}
if (reverseProxyEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        // The proxy must preserve the public Host header. Deliberately do not
        // consume X-Forwarded-Host, so it cannot rewrite OIDC callback origins.
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.RequireHeaderSymmetry = true;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var address in knownProxies)
            options.KnownProxies.Add(address);
    });
}

if (isUiTest)
    builder.WebHost.UseStaticWebAssets();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<OidcSessionTokenStore>();
builder.Services.AddScoped<OidcCookieRefreshEvents>();

var oidcAuthority = builder.Configuration["StudioAuthentication:Authority"];
var oidcClientId = builder.Configuration["StudioAuthentication:ClientId"];
var oidcClientSecret = builder.Configuration["StudioAuthentication:ClientSecret"];
var oidcApiScope = builder.Configuration["StudioAuthentication:ApiScope"];
var oidcClaimsScope = builder.Configuration["StudioAuthentication:ClaimsScope"];
var oidcRequireClientSecret = builder.Configuration.GetValue<bool>("StudioAuthentication:RequireClientSecret");
var oidcRequireHttpsMetadata = builder.Configuration.GetValue<bool?>("StudioAuthentication:RequireHttpsMetadata")
    ?? !builder.Environment.IsDevelopment();
if (!isUiTest && !isLocalDevelopment && (string.IsNullOrWhiteSpace(oidcAuthority) || string.IsNullOrWhiteSpace(oidcClientId)))
{
    throw new InvalidOperationException(
        "StudioAuthentication:Authority and StudioAuthentication:ClientId must be configured before starting VertexBPMN Studio.");
}
if (!isUiTest && !isLocalDevelopment && oidcRequireClientSecret && string.IsNullOrWhiteSpace(oidcClientSecret))
{
    throw new InvalidOperationException(
        "StudioAuthentication:ClientSecret must be supplied by a secret store for this confidential OIDC client.");
}
if (!oidcRequireHttpsMetadata
    && !builder.Environment.IsDevelopment()
    && !(builder.Environment.IsEnvironment("OidcTest") && IsLoopbackHttpAuthority(oidcAuthority)))
{
    throw new InvalidOperationException(
        "StudioAuthentication:RequireHttpsMetadata may be disabled outside Development only for the OidcTest profile with a loopback Authority.");
}

if (isUiTest || isLocalDevelopment)
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = UiTestAuthenticationHandler.Scheme;
        options.DefaultChallengeScheme = UiTestAuthenticationHandler.Scheme;
    })
    .AddScheme<AuthenticationSchemeOptions, UiTestAuthenticationHandler>(
        UiTestAuthenticationHandler.Scheme,
        _ => { });
}
else
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/authentication/login";
        options.LogoutPath = "/authentication/logout";
        options.AccessDeniedPath = "/authentication/access-denied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = oidcRequireHttpsMetadata
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
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
                identity.AddClaim(new Claim(OidcSessionTokenStore.SessionIdClaim, Guid.NewGuid().ToString("N")));
            if (!VertexOidcClaims.TryNormalize(context.Principal!, out var error))
                context.Fail(error);
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
        if (string.IsNullOrWhiteSpace(oidcClaimsScope))
        {
            // Backwards-compatible defaults for existing generic OIDC deployments.
            options.Scope.Add("roles");
            options.Scope.Add("tenant_id");
        }
        else
        {
            options.Scope.Add(oidcClaimsScope);
        }
        if (!string.IsNullOrWhiteSpace(oidcApiScope))
            options.Scope.Add(oidcApiScope);
    });
}
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Configure HTTP clients

var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl) || !Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri))
{
    throw new InvalidOperationException("ApiBaseUrl must be an absolute URI.");
}

builder.Services.AddHttpClient("VertexBPMN.Api", client =>
{
    client.BaseAddress = apiUri;
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<StudioApiAuthorizationHandler>();

builder.Services.AddTransient<StudioApiAuthorizationHandler>();

// Register HTTP-based services
builder.Services.AddScoped<IBpmnEngineService, HttpBpmnEngineService>();
builder.Services.AddScoped<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IEngineAdministrationService, EngineAdministrationService>();
builder.Services.AddScoped<IEngineEventService, EngineEventService>();
builder.Services.AddScoped<IHistoryService, HttpHistoryService>();
builder.Services.AddScoped<IEngineCapabilitiesService, HttpEngineCapabilitiesService>();
builder.Services.AddScoped<IDmnService, HttpDmnService>();
builder.Services.AddScoped<IFormDefinitionService, HttpFormDefinitionService>();
builder.Services.AddScoped<IHealthService, HttpHealthService>();
builder.Services.AddScoped<IAnalyticsService, HttpAnalyticsService>();
builder.Services.AddScoped<IMlAnalyticsService, HttpMlAnalyticsService>();
builder.Services.AddScoped<IFeatureFlagService, HttpFeatureFlagService>();
builder.Services.AddScoped<IIdentityService, HttpIdentityService>();
builder.Services.AddScoped<ICredentialService, HttpCredentialService>();
builder.Services.AddScoped<IPerformanceService, HttpPerformanceService>();
builder.Services.AddScoped<ISimulationService, HttpSimulationService>();
builder.Services.AddScoped<ISimulationScenarioService, HttpSimulationScenarioService>();
builder.Services.AddScoped<IMigrationService, HttpMigrationService>();
builder.Services.AddScoped<IMessageSignalService, HttpMessageSignalService>();
builder.Services.AddScoped<StudioTenantContext>();
builder.Services.AddScoped<IExecutionDetailsService, HttpExecutionDetailsService>();
builder.Services.AddScoped<IPluginService, HttpPluginService>();
builder.Services.AddScoped<IWorkflowTriggerService, HttpWorkflowTriggerService>();

builder.Services.AddScoped<IConnectorService, HttpConnectorService>();
builder.Services.AddScoped<IConnectorTemplateService, HttpConnectorTemplateService>();
builder.Services.AddScoped<IDebuggingService, HttpDebuggingService>();
builder.Services.AddScoped<ICaseManagementService, HttpCaseManagementService>();
builder.Services.AddScoped<NotificationClient>();
builder.Services.AddSingleton<ActiveEngineService>();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();
app.MapDefaultEndpoints();

if (reverseProxyEnabled)
    app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (!isUiTest && httpsRedirectionEnabled)
    app.UseHttpsRedirection();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/oauth2/callback"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
    await next();
});
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/authentication/login", (HttpContext httpContext, string? returnUrl) =>
{
    var target = IsLocalReturnUrl(returnUrl) ? returnUrl! : "/";
    return Results.Challenge(
        new AuthenticationProperties { RedirectUri = target },
        [OpenIdConnectDefaults.AuthenticationScheme]);
}).AllowAnonymous();

app.MapPost("/authentication/logout", async (HttpContext httpContext, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    if (isUiTest || isLocalDevelopment)
        return Results.Redirect("/");

    var tokenStore = httpContext.RequestServices.GetRequiredService<OidcSessionTokenStore>();
    var idToken = tokenStore.GetIdToken(httpContext.User)
        ?? await httpContext.GetTokenAsync("id_token");
    tokenStore.Remove(httpContext.User);
    var logoutProperties = new AuthenticationProperties { RedirectUri = "/" };
    if (!string.IsNullOrWhiteSpace(idToken))
    {
        logoutProperties.StoreTokens(
            [new AuthenticationToken { Name = "id_token", Value = idToken }]);
    }

    return Results.SignOut(
        logoutProperties,
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
}).RequireAuthorization();

app.MapPost("/authentication/session/refresh", async (HttpContext httpContext, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    return Results.Ok(new
    {
        renewed = httpContext.Items.ContainsKey(OidcSessionTokenStore.SessionRenewedItem)
    });
}).RequireAuthorization();

app.MapGet("/authentication/access-denied", () => Results.Problem(
    statusCode: StatusCodes.Status403Forbidden,
    title: "Access denied",
    detail: "The authenticated identity is not authorized to use VertexBPMN Studio.")).AllowAnonymous();

app.MapStaticAssets();
var razorComponents = app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
if (!isUiTest)
    razorComponents.RequireAuthorization();

app.MapControllers();
app.Run();

static bool IsLoopbackHttpAuthority(string? authority) =>
    Uri.TryCreate(authority, UriKind.Absolute, out var uri)
    && uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
    && uri.IsLoopback;

static bool IsLocalReturnUrl(string? returnUrl) =>
    !string.IsNullOrWhiteSpace(returnUrl)
    && returnUrl[0] == '/'
    && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
    && !returnUrl.Contains('\r', StringComparison.Ordinal)
    && !returnUrl.Contains('\n', StringComparison.Ordinal);

public partial class Program;
