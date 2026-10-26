using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Infrastructure;
using VertexBPMN.Infrastructure.Persistence;

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

		services.AddRuntimePersistenceServices(configuration);

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
		var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddRuntimePersistenceServices(configuration));
		Assert.Contains("restricted to Development and Test", error.Message, StringComparison.Ordinal);
	}

	internal static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
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
		var inputs = new Dictionary<string, object>(StringComparer.Ordinal) { ["text"] = "synthetic" };
		var contract = await resolver.ResolveAsync("a", definition, inputs, TestContext.Current.CancellationToken);
		Assert.Equal("v1", contract.Version);
		Assert.Contains("vertex.scalar-contract.v1", contract.SchemaSnapshot, StringComparison.Ordinal);
		await Assert.ThrowsAsync<InvalidOperationException>(async () => await resolver.ResolveAsync("b", definition, inputs, TestContext.Current.CancellationToken));
		await Assert.ThrowsAsync<InvalidOperationException>(async () => await resolver.ResolveAsync("a", definition with { Topic = "other" }, inputs, TestContext.Current.CancellationToken));
		configuration["ExternalTasks:Contracts:0:Enabled"] = "false";
		await Assert.ThrowsAsync<InvalidOperationException>(async () => await resolver.ResolveAsync("a", definition, inputs, TestContext.Current.CancellationToken));
	}

	internal static IConfigurationRoot ObjectConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
	{
		["ExternalTasks:EnableSchedulingPreview"] = "true",
		["ExternalTasks:Contracts:0:TenantId"] = "a",
		["ExternalTasks:Contracts:0:Topic"] = "test.object",
		["ExternalTasks:Contracts:0:Enabled"] = "true",
		["ExternalTasks:Contracts:0:Version"] = "v1",
		["ExternalTasks:Contracts:0:ObjectSchema:Version"] = "vertex.object-contract.v1",
		["ExternalTasks:Contracts:0:ObjectSchema:MaxDepth"] = "3",
		["ExternalTasks:Contracts:0:ObjectSchema:MaxPropertyCount"] = "8",
		["ExternalTasks:Contracts:0:ObjectSchema:MaxItemCount"] = "32",
		["ExternalTasks:Contracts:0:ObjectSchema:MaxBytes"] = "4096",
		["ExternalTasks:Contracts:0:ObjectSchema:Properties:0:Name"] = "text",
		["ExternalTasks:Contracts:0:ObjectSchema:Properties:0:Type"] = "string",
		["ExternalTasks:Contracts:0:ObjectSchema:Properties:0:Required"] = "true",
		["ExternalTasks:Contracts:0:ObjectSchema:Properties:0:MaxLength"] = "64",
		["ExternalTasks:Contracts:0:ObjectSchema:Properties:0:AllowExternalTransfer"] = "true",
		["ExternalTasks:Contracts:0:ObjectSchema:Properties:1:Name"] = "meta",
		["ExternalTasks:Contracts:0:ObjectSchema:Properties:1:Type"] = "object",
		["ExternalTasks:Contracts:0:ObjectSchema:Properties:1:Required"] = "false",
		["ExternalTasks:Contracts:0:ObjectSchema:Properties:1:AllowExternalTransfer"] = "true",
		["ExternalTasks:Contracts:0:BusinessErrorCodes:0"] = "contract_rejected"
	}).Build();

	[Fact]
	public async Task ObjectContractValidatesDepthSizeTypeAndAllowlist()
	{
		var resolver = new ConfiguredExternalTaskContractResolver(ObjectConfiguration());
		var definition = new ExternalTaskDefinition("test.object", null, 0, 300);

		var inputs = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["payload"] = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["text"] = "synthetic",
				["meta"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["n"] = 1 }
			}
		};
		var contract = await resolver.ResolveAsync("a", definition, inputs, TestContext.Current.CancellationToken);
		Assert.Equal("v1", contract.Version);
		Assert.Contains("vertex.object-contract.v1", contract.SchemaSnapshot, StringComparison.Ordinal);
		Assert.Contains("contract_rejected", contract.SchemaSnapshot, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("too-deep")]
	[InlineData("too-many-properties")]
	[InlineData("too-large")]
	[InlineData("wrong-type")]
	[InlineData("missing-required")]
	[InlineData("unknown-property")]
	[InlineData("scalar-root")]
	public async Task ObjectContractRejectsStructuralViolations(string scenario)
	{
		var resolver = new ConfiguredExternalTaskContractResolver(ObjectConfiguration());
		var definition = new ExternalTaskDefinition("test.object", null, 0, 300);
		var inputs = new Dictionary<string, object>(StringComparer.Ordinal);
		switch (scenario)
		{
			case "too-deep":
				inputs["payload"] = Deep(Nested(), 6);
				break;
			case "too-many-properties":
				inputs["payload"] = Enumerable.Range(0, 9).ToDictionary(i => "k" + i, i => (object)i, StringComparer.Ordinal);
				break;
			case "too-large":
				inputs["payload"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["text"] = new string('x', 5000) };
				break;
			case "wrong-type":
				inputs["payload"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["text"] = 42 };
				break;
			case "missing-required":
				inputs["payload"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["meta"] = new Dictionary<string, object>(StringComparer.Ordinal) };
				break;
			case "unknown-property":
				inputs["payload"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["text"] = "x", ["bogus"] = 1 };
				break;
			case "scalar-root":
				inputs["payload"] = "not-an-object";
				break;
		}
		await Assert.ThrowsAsync<InvalidOperationException>(async () => await resolver.ResolveAsync("a", definition, inputs, TestContext.Current.CancellationToken));
	}

	// Adds depth layers of {"nested": {...}} around value.
	private static object Deep(object value, int layers)
	{
		for (var i = 0; i < layers; i++)
		{
			value = new Dictionary<string, object>(StringComparer.Ordinal) { ["nested"] = value };
		}
		return value;
	}

	private static Dictionary<string, object> Nested() => new(StringComparer.Ordinal) { ["text"] = "x" };

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
		var inputs = new Dictionary<string, object>(StringComparer.Ordinal) { ["text"] = "synthetic" };
		var definition = new ExternalTaskDefinition("test.work", null, 0, 300);
		switch (scenario)
		{
			case "wrong-type":
				inputs["text"] = 1;
				break;
			case "oversize":
				inputs["text"] = new string('x', 17);
				break;
			case "missing":
				inputs.Clear();
				break;
			case "extra":
				inputs["secret"] = "not-allowed";
				break;
			case "credential-object":
				inputs["text"] = new { credentialRef = "secret" };
				break;
			case "attempts":
				definition = definition with { MaxRetries = 1 };
				break;
			case "deadline":
				definition = definition with { DeadlineSeconds = 301 };
				break;
			case "transfer-not-approved":
				configuration["ExternalTasks:Contracts:0:Inputs:0:AllowExternalTransfer"] = "false";
				break;
			case "unknown-profile":
				definition = definition with { AgentProfileRef = "unknown" };
				break;
		}
		await Assert.ThrowsAsync<InvalidOperationException>(async () => await new ConfiguredExternalTaskContractResolver(configuration)
			.ResolveAsync("a", definition, inputs, TestContext.Current.CancellationToken));
	}
}
