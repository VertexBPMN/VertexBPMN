using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Application;
using VertexBPMN.Application.Extensions;
using VertexBPMN.Application.Handlers;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Integration.Handlers;

// Passe diesen Namespace an, falls n�tig

/// <summary>
/// Unit-Tests f�r SemanticKernelServiceTaskHandler mit Mocking der IKernelFactory.
/// </summary>
public sealed class SemanticKernelServiceTaskHandlerTests : IDisposable
{
	public void Dispose() => _httpClient.Dispose();

	private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
	private readonly HttpClient _httpClient;

	public SemanticKernelServiceTaskHandlerTests()
	{
		_httpMessageHandlerMock = new Mock<HttpMessageHandler>();
		_httpClient = new HttpClient(_httpMessageHandlerMock.Object);
	}

	[Theory]
	[InlineData("OpenAI", "gpt-4o-mini", "Hallo, wie kann ich helfen?", "llmResult", "Fake-OpenAI-Antwort")]
	[InlineData("Ollama", "llama2", "Was ist BPMN?", "llmResult", "Fake-Ollama-Antwort")]
	public async Task ServiceTaskHandler_SetsResultVariable_ForVariousProviders(
		string provider, string modelId, string promptText, string resultVariable, string expectedResult)
	{
		// ARRANGE

		// 1. Mocke den untersten Service (die eigentliche KI-Arbeit).
		var mockChatService = new Mock<IChatCompletionService>();
		mockChatService
			.Setup(s => s.GetChatMessageContentsAsync(
				It.IsAny<ChatHistory>(),
				It.IsAny<PromptExecutionSettings>(),
				It.IsAny<Kernel>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([new(AuthorRole.Assistant, expectedResult)]);

		// 2. Erstelle einen echten Kernel, der den gemockten Service enth�lt.
		var kernelBuilder = Kernel.CreateBuilder();
		kernelBuilder.Services.AddSingleton<IChatCompletionService>(mockChatService.Object);
		var kernel = kernelBuilder.Build();

		// 3. Mocke die KernelFactory.
		var mockKernelFactory = new Mock<IKernelFactory>();

		// 4. Bringe der gemockten Factory bei, unseren pr�parierten Kernel zur�ckzugeben,
		//    wenn ihre GetKernel-Methode aufgerufen wird.
		mockKernelFactory
			.Setup(f => f.GetKernel(It.IsAny<IDictionary<string, string>>()))
			.Returns(kernel);

		// 5. Erstelle die Instanz des Handlers mit der gemockten Factory.
		var handler = new SemanticKernelServiceTaskHandler(mockKernelFactory.Object);

		// Die BPMN-Attribute und Prozessvariablen bleiben gleich.
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "provider", provider },
			{ "modelId", modelId },
			{ "prompt", "customerMessage" },
			{ "resultVariable", resultVariable }
		};
		var processVariables = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			{ "customerMessage", promptText }
		};

		// ACT
		await handler.ExecuteAsync(attributes, processVariables, TestContext.Current.CancellationToken);

		// ASSERT
		Assert.True(processVariables.ContainsKey(resultVariable));
		Assert.Equal(expectedResult, processVariables[resultVariable]);

		// Optional, aber empfohlen: Verifiziere, dass die Factory aufgerufen wurde.
		mockKernelFactory.Verify(f => f.GetKernel(attributes), Times.Once);
	}

	[Fact]
	public async Task Executes_MultiInstanceSubprocess_FlowsToEnd()
	{
		// Arrange
		var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "SemanticKernelTestProcess.bpmn");
		var xml = await File.ReadAllTextAsync(bpmnFile, TestContext.Current.CancellationToken);
		var logger = new Mock<ILogger<BpmnParser>>();
		var parser = new BpmnParser(logger.Object, TracerProvider.Default);
		var processVariables = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			{ "customerMessage", "Wie ist das Wetter heute?" }
		};
		var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
		model = model with { ProcessVariables = processVariables };
		Assert.NotNull(model);

		Assert.NotNull(model.Tasks);
		var task = Assert.Single(model.Tasks);
		Assert.Equal("serviceTask", task.Type);
		Assert.Equal("semanticKernelServiceTask", task.Implementation);
		Assert.NotNull(task.Attributes);
		Assert.Equal("OpenAI", task.Attributes["provider"]);
		Assert.Equal("gpt-4o", task.Attributes["modelId"]);
		Assert.Equal("customerMessage", task.Attributes["prompt"]);
		Assert.Equal("llmResult", task.Attributes["resultVariable"]);

		// Set up the service collection and registry
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddServiceTaskHandlers(); // Use the updated ServiceTaskRegistryExtensions

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IServiceTaskRegistry>();
		registry.Register("semanticKernelServiceTask", new FakeSemanticKernelHandler());
		// Verify that the registry resolves the handler correctly
		Assert.True(registry.TryResolve("semanticKernelServiceTask", out var semanticKernelHandler));
		Assert.NotNull(semanticKernelHandler);

		var engine = new ProcessEngine(NullLogger<ProcessEngine>.Instance, registry);

		// Act
		var trace = await engine.ExecuteAsync(model, TestContext.Current.CancellationToken);

		// Assert
		Assert.Contains(trace, r => r.Contains("ServiceTask: SK_ServiceTask_1", StringComparison.Ordinal));
		Assert.Contains(trace, r => r.Contains("EndEvent: EndEvent_1", StringComparison.Ordinal));
		Assert.True(model.ProcessVariables.ContainsKey("customerMessage"));
		Assert.Equal("Das Wetter ist sonnig.", model.ProcessVariables["llmResult"]);
	}

	[Fact]
	public async Task Parse_ServiceTask_WithAttributesAndExtensionElements()
	{
		// Arrange: Minimal BPMN XML with serviceTask
		const string xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\">\n  <bpmn:process id=\"TestProcess\" name=\"Test Process\" isExecutable=\"true\">\n    <bpmn:serviceTask id=\"ServiceTask_1\" name=\"AI Task\" implementation=\"semanticKernelServiceTask\">\n      <bpmn:extensionElements>\n        <bpmn:property name=\"provider\" value=\"OpenAI\"/>\n        <bpmn:property name=\"modelId\" value=\"gpt-4o\"/>\n        <bpmn:property name=\"prompt\" value=\"customerMessage\"/>\n        <bpmn:property name=\"resultVariable\" value=\"llmResult\"/>\n      </bpmn:extensionElements>\n    </bpmn:serviceTask>\n  </bpmn:process>\n</bpmn:definitions>";

		var logger = new Mock<ILogger<BpmnParser>>();
		var parser = new BpmnParser(logger.Object, TracerProvider.Default);

		// Act
		var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
		// Assert
		Assert.NotNull(model.Tasks);
		var task = Assert.Single(model.Tasks);
		Assert.Equal("serviceTask", task.Type);
		Assert.Equal("semanticKernelServiceTask", task.Implementation);
		Assert.NotNull(task.Attributes);
		Assert.Equal("OpenAI", task.Attributes["provider"]);
		Assert.Equal("gpt-4o", task.Attributes["modelId"]);
		Assert.Equal("customerMessage", task.Attributes["prompt"]);
		Assert.Equal("llmResult", task.Attributes["resultVariable"]);
	}

	private sealed class FakeSemanticKernelHandler : IServiceTaskHandler
	{
		public Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default)
		{
			variables["llmResult"] = "Das Wetter ist sonnig.";
			return Task.CompletedTask;
		}
	}
}

