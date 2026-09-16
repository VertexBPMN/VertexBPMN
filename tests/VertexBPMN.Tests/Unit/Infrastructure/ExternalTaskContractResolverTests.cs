using Microsoft.Extensions.Configuration;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class ExternalTaskContractResolverTests
{
    [Fact]
    public void ProductionExternalTasksRegisterCoreServicesWithoutEnablingPreview()
    {
        var configuration = Configuration();
        configuration["OperationalMode"] = "Production";
        configuration["ExternalTasks:Enabled"] = "true";
        configuration["ExternalTasks:EnableSchedulingPreview"] = "false";
        configuration["ConnectionStrings:DependencyRegistry"] = "Data Source=production-test.db";
        configuration["Runtime:Outbox:Enabled"] = "true";
        configuration["Runtime:Outbox:Provider"] = "RabbitMq";
        configuration["Runtime:Outbox:ConnectionString"] = "amqp://localhost";
        configuration["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-external-task-registration");
        var services = new ServiceCollection();

        services.AddBpmnPersistenceServices(configuration);

        Assert.Contains(services, item => item.ServiceType == typeof(IExternalTaskLeaseService));
        Assert.Contains(services, item => item.ServiceType == typeof(IExternalTaskRecoveryService));
        Assert.Contains(services, item => item.ServiceType == typeof(IExternalTaskContractResolver));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Stage")]
    [InlineData("unknown")]
    [InlineData(null)]
    public void PreviewRegistrationRequiresExplicitLocalEnvironment(string? environment)
    {
        var configuration = Configuration();
        configuration["OperationalMode"] = environment;
        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddBpmnPersistenceServices(configuration));
        Assert.Contains("restricted to Development and Test", error.Message);
    }

    internal static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ExternalTasks:EnableSchedulingPreview"] = "true",
        ["ExternalTasks:Contracts:0:TenantId"] = "a",
        ["ExternalTasks:Contracts:0:Topic"] = "test.work",
        ["ExternalTasks:Contracts:0:Enabled"] = "true",
        ["ExternalTasks:Contracts:0:Version"] = "v1",
        ["ExternalTasks:Contracts:0:Inputs:0:Name"] = "text",
        ["ExternalTasks:Contracts:0:Inputs:0:MaxLength"] = "16",
        ["ExternalTasks:Contracts:0:Inputs:0:AllowExternalTransfer"] = "true",
        ["ExternalTasks:Contracts:0:Outputs:0:Name"] = "approved",
        ["ExternalTasks:Contracts:0:Outputs:0:Type"] = "boolean",
        ["ExternalTasks:Contracts:0:Outputs:0:Required"] = "true",
        ["ExternalTasks:Contracts:0:Outputs:0:AllowExternalTransfer"] = "true",
        ["ExternalTasks:Contracts:0:BusinessErrorCodes:0"] = "contract_rejected"
    }).Build();

    [Fact]
    public async Task MatchesExactTenantAndTopicAndObservesRevocation()
    {
        var configuration = Configuration();
        var resolver = new ConfiguredExternalTaskContractResolver(configuration);
        var definition = new ExternalTaskDefinition("test.work", null, 0, 300);
        var inputs = new Dictionary<string, object> { ["text"] = "synthetic" };
        var contract = await resolver.ResolveAsync("a", definition, inputs, TestContext.Current.CancellationToken);
        Assert.Equal("v1", contract.Version);
        Assert.Contains("vertex.scalar-contract.v1", contract.SchemaSnapshot);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await resolver.ResolveAsync("b", definition, inputs, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await resolver.ResolveAsync("a", definition with { Topic = "other" }, inputs, TestContext.Current.CancellationToken));
        configuration["ExternalTasks:Contracts:0:Enabled"] = "false";
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await resolver.ResolveAsync("a", definition, inputs, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("wrong-type")]
    [InlineData("oversize")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("credential-object")]
    [InlineData("attempts")]
    [InlineData("deadline")]
    [InlineData("transfer-not-approved")]
    [InlineData("unknown-profile")]
    public async Task RejectsInvalidInputAndPolicy(string scenario)
    {
        var configuration = Configuration();
        var inputs = new Dictionary<string, object> { ["text"] = "synthetic" };
        var definition = new ExternalTaskDefinition("test.work", null, 0, 300);
        switch (scenario)
        {
            case "wrong-type": inputs["text"] = 1; break;
            case "oversize": inputs["text"] = new string('x', 17); break;
            case "missing": inputs.Clear(); break;
            case "extra": inputs["secret"] = "not-allowed"; break;
            case "credential-object": inputs["text"] = new { credentialRef = "secret" }; break;
            case "attempts": definition = definition with { MaxRetries = 1 }; break;
            case "deadline": definition = definition with { DeadlineSeconds = 301 }; break;
            case "transfer-not-approved": configuration["ExternalTasks:Contracts:0:Inputs:0:AllowExternalTransfer"] = "false"; break;
            case "unknown-profile": definition = definition with { AgentProfileRef = "unknown" }; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await new ConfiguredExternalTaskContractResolver(configuration)
            .ResolveAsync("a", definition, inputs, TestContext.Current.CancellationToken));
    }
}
