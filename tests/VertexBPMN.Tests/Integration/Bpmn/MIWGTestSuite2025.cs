using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Integration.Bpmn
{
    public class MIWGTestSuite2025
    {
        public static IEnumerable<object[]> GetBpmnFiles()
        {
            var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
           var dir = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference");
            Console.WriteLine($"MIWG BPMN test dir: {dir}");

            if (!Directory.Exists(dir)) yield break;
            foreach (var file in Directory.GetFiles(dir, "*.bpmn").OrderBy(f => f).Take(1))
            {
                yield return new object[] { file };
            }
        }

        [Theory]
        [MemberData(nameof(GetBpmnFiles))]
        public async Task Engine_Should_Execute_MIWG_Bpmn_File(string bpmnFile)
        {
            var xml = File.ReadAllText(bpmnFile);
           var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model =  await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, $"No trace produced for {Path.GetFileName(bpmnFile)}");
            // Optionally: Add more assertions for expected events, tokens, etc.
        }
    }
}
