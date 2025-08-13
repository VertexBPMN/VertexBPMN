using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Tests.Bpmn
{
    public class MIWGTestSuite2025Roundtrip
    {
        public static IEnumerable<object[]> GetBpmnFiles()
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference");
            Console.WriteLine($"MIWG BPMN test dir: {dir}");
            if (!Directory.Exists(dir))
            {
                Assert.Fail($"MIWG BPMN test directory does not exist.\ndir: {dir}");
                yield break;
            }
            var files = Directory.GetFiles(dir, "*.bpmn");
            Console.WriteLine($"Found {files.Length} BPMN files.");
            if (files.Length == 0)
            {
                var entries = Directory.Exists(dir) ? string.Join("\n", Directory.GetFileSystemEntries(dir)) : "(directory does not exist)";
                var msg = $"No BPMN files found.\ndir: {dir}\nDirectory contents:\n{entries}";
                Assert.Fail(msg);
            }
            foreach (var file in files.Take(1)) // Limit to 1 file for testing
            {
                Console.WriteLine($"Test file: {file}");
                yield return new object[] { file };
            }
        }

        [Theory]
        [MemberData(nameof(GetBpmnFiles))]
        public void Engine_Should_Import_Export_Roundtrip_Bpmn_File(string bpmnFile)
        {
            var xmlOriginal = File.ReadAllText(bpmnFile);
            var parser = new BpmnParser();
            var model = parser.Parse(xmlOriginal);
            var engine = new TokenEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, $"No trace produced for {Path.GetFileName(bpmnFile)}");

            // Export: Serialize model back to BPMN XML
            var xmlExported = parser.Serialize(model);
            Assert.False(string.IsNullOrWhiteSpace(xmlExported), $"Exported BPMN XML is empty for {Path.GetFileName(bpmnFile)}");

            // Roundtrip: Parse exported XML and compare structure
            var modelRoundtrip = parser.Parse(xmlExported);
            Assert.NotNull(modelRoundtrip);
            // Optionally: Compare key model properties for equality
            Assert.Equal(model.ProcessId, modelRoundtrip.ProcessId);
            Assert.Equal(model.Activities.Count(), modelRoundtrip.Activities.Count());
            Assert.Equal(model.Gateways.Count, modelRoundtrip.Gateways.Count);
            // Optionally: Compare XMLs (ignoring whitespace/ordering)
            // Assert.True(XmlEquals(xmlOriginal, xmlExported), $"Roundtrip XML mismatch for {Path.GetFileName(bpmnFile)}");
        }
    }
}
