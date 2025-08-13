using System.IO;
using Xunit;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Tests.TestSuite2025
{
    public class C_8_1_Test
    {
        [Fact]
        public void Test_C_8_1_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.8.1.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var parser = new BpmnParser();
            var model = parser.Parse(xml);
            Assert.NotNull(model);
            var engine = new TokenEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.8.1.bpmn");
                var types = result.Select(x => x.Split(':')[0].Trim()).ToList();
                Assert.Contains("StartEvent", types);
                Assert.Contains("UserTask", types);
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
