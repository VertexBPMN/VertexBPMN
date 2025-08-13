using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Tests.Bpmn
{
    public class MIWGTestSuite2025
    {
        public static IEnumerable<object[]> GetBpmnFiles()
        {
            var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
           var dir = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference");
            Console.WriteLine($"MIWG BPMN test dir: {dir}");

            if (!Directory.Exists(dir)) yield break;
            foreach (var file in Directory.GetFiles(dir, "*.bpmn").Take(1))
            {
                yield return new object[] { file };
            }
        }

        [Theory]
        [MemberData(nameof(GetBpmnFiles))]
        public void Engine_Should_Execute_MIWG_Bpmn_File(string bpmnFile)
        {
            var xml = File.ReadAllText(bpmnFile);
            var parser = new BpmnParser();
            var model = parser.Parse(xml);
            var engine = new TokenEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, $"No trace produced for {Path.GetFileName(bpmnFile)}");
            // Optionally: Add more assertions for expected events, tokens, etc.
        }
    }
}
