using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VertexBPMN.AgentWorker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOptions<ExternalTaskWorkerOptions>()
    .Bind(builder.Configuration.GetSection(ExternalTaskWorkerOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ExternalTaskWorkerOptions>, ExternalTaskWorkerOptionsValidator>();
builder.Services.AddHttpClient("VertexBPMN.ExternalTasks");
builder.Services.AddHttpClient("VertexBPMN.WorkerToken");
builder.Services.AddSingleton<IWorkerAccessTokenProvider, OAuthClientCredentialsTokenProvider>();
builder.Services.AddSingleton<IExternalTaskApiClient, HttpExternalTaskApiClient>();
builder.Services.AddHostedService<ExternalTaskWorkerService>();
await builder.Build().RunAsync();
