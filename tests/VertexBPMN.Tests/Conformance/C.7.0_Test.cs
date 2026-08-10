using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Conformance
{
    public class C_7_0_Test
    {
        //[Fact(Skip = "BPMN 2.0 C.7.0 test not implemented, is too complex and slow")]
        [Fact]
        public void Test_C_7_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.7.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            Assert.Throws<SecurityException>(() =>parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult());
            /*var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            Assert.Throws<SecurityException>(() => engine.Execute(model));
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.7.0.bpmn");
            foreach (var item in result)
            {
                Console.WriteLine($"Result item: {item}");
            }*/
        }
    }
}
