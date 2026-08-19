using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Application.Configuration;
using VertexBPMN.Application.Import;
using VertexBPMN.Cli;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Cli;

public sealed class CliApplicationTests
{
    private const string BpmnXml = """<definitions id="OrderProcess"></definitions>""";

    [Fact]
    public async Task DeployBpmn_UsesFileArgumentAndOptionalTenant()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vertexbpmn-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var bpmnPath = Path.Combine(tempDir, "order-process.bpmn");
            await File.WriteAllTextAsync(bpmnPath, BpmnXml, TestContext.Current.CancellationToken);

            var definitionId = Guid.NewGuid();
            var repository = new Mock<IRepositoryService>(MockBehavior.Strict);
            repository
                .Setup(service => service.DeployAsync(
                    BpmnXml,
                    "order-process.bpmn",
                    "tenant-a",
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<ProcessDefinition>(new ProcessDefinition
                {
                    Id = definitionId,
                    Key = "OrderProcess"
                }));

            using var output = new StringWriter();
            using var error = new StringWriter();
            var application = new CliApplication(CreateServices(repository.Object), output, error);

            var exitCode = await application.RunAsync(new[] { "deploy-bpmn", bpmnPath, "tenant-a" }, TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.Contains($"BPMN deployed: OrderProcess ({definitionId})", output.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
            repository.VerifyAll();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Help_AdvertisesPhaseTwelveCommands()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(CreateServices(Mock.Of<IRepositoryService>()), output, error);

        var exitCode = await application.RunAsync(new[] { "--help" }, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("deploy-bpmn <bpmn-file> [tenant]", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("deploy-dmn <dmn-file> [tenant]", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("deploy-form <form-json> [tenant]", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("test-run <bpmn-file> <variables-json> [tenant]", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("credential create|list|rotate", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("connector create|list|test", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("import-n8n <workflow-json> [output-bpmn]", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    private static IServiceProvider CreateServices(IRepositoryService repositoryService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IProcessEngine>());
        services.AddSingleton(Mock.Of<IBpmnParser>());
        services.AddSingleton(Mock.Of<ICmmnParser>());
        services.AddSingleton(Mock.Of<IWorkerNodeManager>());
        services.AddSingleton(Mock.Of<IDependencyRegistry>());
        services.AddSingleton(Mock.Of<IWorkflowTriggerService>());
        services.AddSingleton(Mock.Of<IRuntimeService>());
        services.AddSingleton(Mock.Of<ICredentialService>());
        services.AddSingleton(Mock.Of<IConnectorService>());
        services.AddSingleton(Mock.Of<IConnectorTemplateService>());
        services.AddSingleton(Mock.Of<IFormDefinitionService>());
        services.AddSingleton(Mock.Of<IDecisionService>());
        services.AddSingleton(Mock.Of<ISemanticValidationService>());
        services.AddSingleton<IN8nWorkflowImporter, N8nWorkflowImporter>();
        services.AddSingleton(repositoryService);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(DashboardLauncherFactory);
        return services.BuildServiceProvider();
    }

    private static DashboardLauncher DashboardLauncherFactory(IServiceProvider services) =>
        new(
            services.GetRequiredService<IConfiguration>(),
            NullLogger<DashboardLauncher>.Instance);
}
