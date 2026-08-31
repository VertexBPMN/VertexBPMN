using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Dmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class C_8_1_Test
    {
        [Fact]
        public async Task Test_C_8_1_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.8.1.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var mockDecision = new Mock<IDecisionService>();
            mockDecision.Setup(d => d.EvaluateDecisionByKeyAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DecisionResult(new Dictionary<string, object> { ["Vacation Approval"] = "Manual Validation Required" }));

            var parserLogger = new Mock<ILogger<BpmnParser>>();
            // KORREKTUR: vorher `new BpmnParser()` ohne Logger/TracerProvider – inkonsistent
            // zu allen anderen Tests dieser Suite.
            var parser = new BpmnParser(parserLogger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
            Assert.NotNull(model);
            var engine = new ProcessEngine(Mock.Of<ILogger<ProcessEngine>>(),
                NullServiceTaskRegistry.Instance, mockDecision.Object);

            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.8.1.bpmn");
            var types = result.Select(x => x.Split(':')[0].Trim()).ToList();
            Assert.Contains("StartEvent", types);
            Assert.Contains("UserTask", types);
            Assert.Contains(result, entry => entry.Contains(
                "ExclusiveFlowSelected: _0a1c4f20-509f-4aeb-baf9-acc762f4fdf9",
                StringComparison.Ordinal));
            Assert.Contains("BusinessRuleTask", types);  // Verify the rule eval happens
            Assert.Contains("SequenceFlow", types);
            Assert.Contains("ExclusiveGateway", types);
            Assert.Contains("EndEvent", types);
            foreach (var item in result)
            {
                Console.WriteLine($"Result item: {item}");
            }
        }
    }
}
