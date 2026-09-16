using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using VertexBPMN.AgentWorker;

var builder = Host.CreateApplicationBuilder(args);
// Worker processes run in containers and as unprivileged local Windows processes.
// The default Windows EventLog provider can throw on every log write when the
// process cannot register/open an event source, so keep the worker on portable
// console output and OpenTelemetry instead.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.AddServiceDefaults();
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(AgentWorkerTelemetry.MeterName));
builder.Services.AddOptions<ExternalTaskWorkerOptions>()
    .Bind(builder.Configuration.GetSection(ExternalTaskWorkerOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ExternalTaskWorkerOptions>, ExternalTaskWorkerOptionsValidator>();
builder.Services.AddOptions<ContractReviewerOptions>()
    .Bind(builder.Configuration.GetSection(ContractReviewerOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ContractReviewerOptions>, ContractReviewerOptionsValidator>();
builder.Services.AddHttpClient("VertexBPMN.ExternalTasks");
builder.Services.AddHttpClient("VertexBPMN.WorkerToken");
// Deliberately do not use the shared HttpClientFactory defaults here: local
// inference commonly exceeds their 30-second total timeout. The adapter owns
// a bounded timeout through ContractReviewer:MaximumRuntimeSeconds.
builder.Services.AddSingleton(_ => new HttpClient(LocalModelHttpHandler.Create(), disposeHandler: true)
{
    Timeout = Timeout.InfiniteTimeSpan
});
builder.Services.AddSingleton<IWorkerAccessTokenProvider, OAuthClientCredentialsTokenProvider>();
builder.Services.AddSingleton<IExternalTaskApiClient, HttpExternalTaskApiClient>();
builder.Services.AddSingleton<IAgentRuntime, OllamaAgentRuntime>();
builder.Services.AddSingleton<IExternalTaskHandler, ContractReviewExternalTaskHandler>();
builder.Services.AddHostedService<ExternalTaskWorkerService>();
await builder.Build().RunAsync();
