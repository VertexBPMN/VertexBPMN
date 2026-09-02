using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Application.Extensions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class C_9_0_Test
    {
        [Fact]
        public async Task Test_C_9_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.9.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
            Assert.NotNull(model);
            model = model with
            {
                ProcessVariables = new Dictionary<string, object>
                {
                    ["riskLevels"] = new[] { "red" },
                    ["approved"] = false
                }
            };
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceTaskHandlers();
            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<IServiceTaskRegistry>();

            // Act & Assert
            Assert.True(registry.TryResolve("calculateScore", out var calculateScoreHandler));
            Assert.NotNull(calculateScoreHandler);
            var engine = new ProcessEngine(NullLogger<ProcessEngine>.Instance, registry);
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.9.0.bpmn");

            // Ergänzt: "Customer Onboarding" (Zeebe-Demoprozess) – Referenzmodell enthält
            // u. a. ServiceTask (zeebe:taskDefinition type="calculateScore"), BusinessRuleTask,
            // UserTask, SendTask, CallActivity, ExclusiveGateway, ParallelGateway, BoundaryEvent
            // (Error), SubProcess. Bewusst KEIN "UserTask" ausgeschlossen wie in C.8.0/C.8.1,
            // da hier laut Modell tatsächlich ein UserTask ("Manual Check") vorkommt.
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("ExclusiveGateway"));
            // ACHTUNG: folgende Bezeichner bisher unbestätigtes Vokabular – ggf. anpassen,
            // falls euer Trace andere Namen nutzt (z. B. "ServiceTask" vs. konkreter Handler-Name).
            Assert.Contains(result, r => r.ToString().Contains("ServiceTask"));
            Assert.Contains(result, r => r.ToString().Contains("BusinessRuleTask"));
            Assert.Contains(result, r => r.Contains("ExclusiveFlowSelected: SequenceFlow_Red", StringComparison.Ordinal));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
            var scopedEventStarts = model.Events.Where(evt =>
                evt.Type == "startEvent" && evt.Definitions is { Count: > 0 }).ToArray();
            Assert.DoesNotContain(scopedEventStarts, evt =>
                result.Any(entry => entry.Contains($"StartEvent: {evt.Id}", StringComparison.Ordinal)));
        }
    }
}
