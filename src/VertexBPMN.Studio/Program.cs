using VertexBPMN.Studio.Components;
using VertexBPMN.Studio;
using VertexBPMN.Studio.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);
var isUiTest = builder.Environment.IsEnvironment("UiTest")
    && string.Equals(
        builder.Configuration["StudioAuthentication:UiTestEnabled"],
        "true",
        StringComparison.OrdinalIgnoreCase);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();

var oidcAuthority = builder.Configuration["StudioAuthentication:Authority"];
var oidcClientId = builder.Configuration["StudioAuthentication:ClientId"];
var oidcClientSecret = builder.Configuration["StudioAuthentication:ClientSecret"];
var oidcApiScope = builder.Configuration["StudioAuthentication:ApiScope"];
if (!isUiTest && (string.IsNullOrWhiteSpace(oidcAuthority) || string.IsNullOrWhiteSpace(oidcClientId)))
{
    throw new InvalidOperationException(
        "StudioAuthentication:Authority and StudioAuthentication:ClientId must be configured before starting VertexBPMN Studio.");
}

if (isUiTest)
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
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = oidcAuthority;
        options.ClientId = oidcClientId;
        options.ClientSecret = oidcClientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("roles");
        options.Scope.Add("tenant_id");
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
.AddHttpMessageHandler<StudioApiAuthorizationHandler>()
.AddPolicyHandler(GetRetryPolicy());

builder.Services.AddTransient<StudioApiAuthorizationHandler>();

builder.Services.AddHttpClient("Default", client =>
{
    client.BaseAddress = new Uri("http://localhost/");
});

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

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
builder.Services.AddScoped<IHealthService, HttpHealthService>();
builder.Services.AddScoped<IAnalyticsService, HttpAnalyticsService>();
builder.Services.AddScoped<IMlAnalyticsService, HttpMlAnalyticsService>();
builder.Services.AddScoped<IFeatureFlagService, HttpFeatureFlagService>();
builder.Services.AddScoped<IIdentityService, HttpIdentityService>();
builder.Services.AddScoped<IPerformanceService, HttpPerformanceService>();
builder.Services.AddScoped<ISimulationService, HttpSimulationService>();
builder.Services.AddScoped<ISimulationScenarioService, HttpSimulationScenarioService>();
builder.Services.AddScoped<IMigrationService, HttpMigrationService>();
builder.Services.AddScoped<IMessageSignalService, HttpMessageSignalService>();
builder.Services.AddScoped<StudioTenantContext>();
builder.Services.AddScoped<IExecutionDetailsService, HttpExecutionDetailsService>();
builder.Services.AddScoped<IPluginService, HttpPluginService>();
builder.Services.AddScoped<IDebuggingService, HttpDebuggingService>();
builder.Services.AddScoped<ICaseManagementService, GrpcCaseManagementService>();
builder.Services.AddScoped<NotificationClient>();
builder.Services.AddSingleton<ActiveEngineService>();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (!isUiTest)
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/authentication/login", (HttpContext httpContext, string? returnUrl) =>
{
    var target = string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/')
        ? "/"
        : returnUrl;
    return Results.Challenge(
        new AuthenticationProperties { RedirectUri = target },
        [OpenIdConnectDefaults.AuthenticationScheme]);
}).AllowAnonymous();

app.MapGet("/authentication/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await httpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = "/" });
}).AllowAnonymous();

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

public partial class Program;
