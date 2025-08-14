using Microsoft.Extensions.DependencyInjection;

namespace VertexBPMN.Tests.Tasks;


using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Tasks; // Passe diesen Namespace an, falls nötig
using Xunit;

/// <summary>
/// Unit-Tests für SemanticKernelServiceTaskHandler mit Mocking der IKernelFactory.
/// </summary>
public class SemanticKernelServiceTaskHandlerTests
{
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

        // 2. Erstelle einen echten Kernel, der den gemockten Service enthält.
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton<IChatCompletionService>(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        // 3. Mocke die KernelFactory.
        var mockKernelFactory = new Mock<IKernelFactory>();

        // 4. Bringe der gemockten Factory bei, unseren präparierten Kernel zurückzugeben,
        //    wenn ihre GetKernel-Methode aufgerufen wird.
        mockKernelFactory
            .Setup(f => f.GetKernel(It.IsAny<IDictionary<string, string>>()))
            .Returns(kernel);

        // 5. Erstelle die Instanz des Handlers mit der gemockten Factory.
        var handler = new SemanticKernelServiceTaskHandler(mockKernelFactory.Object);

        // Die BPMN-Attribute und Prozessvariablen bleiben gleich.
        var attributes = new Dictionary<string, string>
        {
            { "provider", provider },
            { "modelId", modelId },
            { "prompt", "customerMessage" },
            { "resultVariable", resultVariable }
        };
        var processVariables = new Dictionary<string, object>
        {
            { "customerMessage", promptText }
        };

        // ACT
        await handler.ExecuteAsync(attributes, processVariables, CancellationToken.None);

        // ASSERT
        Assert.True(processVariables.ContainsKey(resultVariable));
        Assert.Equal(expectedResult, processVariables[resultVariable]);

        // Optional, aber empfohlen: Verifiziere, dass die Factory aufgerufen wurde.
        mockKernelFactory.Verify(f => f.GetKernel(attributes), Times.Once);
    }

    [Fact]
    public void Executes_MultiInstanceSubprocess_FlowsToEnd()
    {
        var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "SemanticKernelTestProcess.bpmn");
        var xml = File.ReadAllText(bpmnFile);
        var parser = new BpmnParser();
        var processVariables = new Dictionary<string, object>
        {
            { "customerMessage", "Wie ist das Wetter heute?" }
        };
        var model = parser.Parse(xml) with { ProcessVariables = processVariables };
        Assert.NotNull(model);
        var task = Assert.Single(model.Tasks);
        Assert.Equal("serviceTask", task.Type);
        Assert.Equal("semanticKernelServiceTask", task.Implementation);
        Assert.NotNull(task.Attributes);
        Assert.Equal("OpenAI", task.Attributes["provider"]);
        Assert.Equal("gpt-4o", task.Attributes["modelId"]);
        Assert.Equal("customerMessage", task.Attributes["prompt"]);
        Assert.Equal("llmResult", task.Attributes["resultVariable"]);
        var engine = new TokenEngine();
        // Replace the handler with a fake for deterministic test
        var handlerDict = engine.GetType()
            .GetField("_serviceTaskHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(engine) as Dictionary<string, System.Func<IDictionary<string, string>, IDictionary<string, object>, CancellationToken, Task>>;
        if (handlerDict != null)
        {
            handlerDict["semanticKernelServiceTask"] = async (attrs, vars, ct) =>
            {
                vars["llmResult"] = "Das Wetter ist sonnig.";
                await Task.CompletedTask;
            };
        }
        // Act
        var trace = engine.Execute(model);

        // Assert
        Assert.Contains("ServiceTask: SK_ServiceTask_1 (semanticKernelServiceTask)", trace);
        Assert.Contains("ServiceTaskCompleted: SK_ServiceTask_1", trace);
        Assert.True(model.ProcessVariables.ContainsKey("llmResult"));
        Assert.Equal("Das Wetter ist sonnig.", model.ProcessVariables["llmResult"]);
    }

    [Fact]
    public void Parse_ServiceTask_WithAttributesAndExtensionElements()
    {
        // Arrange: Minimal BPMN XML with serviceTask
        var bpmnXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\">\n  <bpmn:process id=\"TestProcess\" name=\"Test Process\" isExecutable=\"true\">\n    <bpmn:serviceTask id=\"ServiceTask_1\" name=\"AI Task\" implementation=\"semanticKernelServiceTask\">\n      <bpmn:extensionElements>\n        <bpmn:property name=\"provider\" value=\"OpenAI\"/>\n        <bpmn:property name=\"modelId\" value=\"gpt-4o\"/>\n        <bpmn:property name=\"prompt\" value=\"customerMessage\"/>\n        <bpmn:property name=\"resultVariable\" value=\"llmResult\"/>\n      </bpmn:extensionElements>\n    </bpmn:serviceTask>\n  </bpmn:process>\n</bpmn:definitions>";

        var parser = new BpmnParser();

        // Act
        var model = parser.Parse(bpmnXml);

        // Assert
        var task = Assert.Single(model.Tasks);
        Assert.Equal("serviceTask", task.Type);
        Assert.Equal("semanticKernelServiceTask", task.Implementation);
        Assert.NotNull(task.Attributes);
        Assert.Equal("OpenAI", task.Attributes["provider"]);
        Assert.Equal("gpt-4o", task.Attributes["modelId"]);
        Assert.Equal("customerMessage", task.Attributes["prompt"]);
        Assert.Equal("llmResult", task.Attributes["resultVariable"]);
    }
}

