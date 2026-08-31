using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Conformance
{
    public class C_8_0_Test
    {
        public async Task Test_C_8_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.8.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser();
            var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.8.0.bpmn");
            var types = result.Select(x => x.Split(':')[0].Trim()).ToList();
            Assert.Contains("StartEvent", types);
            Assert.DoesNotContain("UserTask", types);
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
