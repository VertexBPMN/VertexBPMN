using OpenTelemetry.Trace;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application;
using VertexBPMN.Cli;
using VertexBPMN.Engine;
using VertexBPMN.Infrastructure;
using VertexBPMN.Infrastructure.Persistence;

if (CliApplication.IsHelpRequest(args))
{
    CliApplication.WriteHelp(Console.Out);
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);
DependencyConfigurationLoader.LoadInto(builder.Configuration);
builder.Configuration.AddEnvironmentVariables("VERTEXBPMN_");
builder.Services.AddLogging(logging => logging.AddSimpleConsole(options =>
{
	options.SingleLine = true;
	options.TimestampFormat = "HH:mm:ss ";
}));
builder.Services.AddSingleton<DashboardLauncher>();
builder.Services.AddSingleton<TracerProvider>(TracerProvider.Default);
builder.Services.AddAllEngineDbContexts(builder.Configuration);
builder.Services.AddBpmnPersistenceServices(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddEngineServices(builder.Configuration);

using IHost host = builder.Build();
await host.StartAsync();
using var scope = host.Services.CreateScope();
var application = new VertexBPMN.Cli.CliApplication(scope.ServiceProvider, Console.Out, Console.Error);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
	eventArgs.Cancel = true;
	cancellation.Cancel();
};

var exitCode = await application.RunAsync(args, cancellation.Token);
await host.StopAsync(CancellationToken.None);
return exitCode;
