using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance
{
    public class MIWGTestSuite2025Roundtrip
    {
        private static readonly string[] ExecutionScenarioFiles =
        [
            "A.2.0.bpmn",
            "C.9.1.bpmn",
            "C.1.1.bpmn",
            "C.1.0.bpmn",
            "C.2.0.bpmn"
        ];

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
            foreach (var fileName in ExecutionScenarioFiles)
            {
                var file = Path.Combine(dir, fileName);
                Assert.True(File.Exists(file), $"Required MIWG execution scenario is missing: {fileName}");
                Console.WriteLine($"Test file: {file}");
                yield return new object[] { file };
            }
        }

        [Theory]
        [MemberData(nameof(GetBpmnFiles))]
        public async Task Engine_Should_Import_Export_Roundtrip_Bpmn_File(string bpmnFile)
        {
            var xml = File.ReadAllText(bpmnFile);
            var parser = new BpmnParser();
            var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
            var engine = new ProcessEngine();
            var variables = RuntimeInputsFor(Path.GetFileName(bpmnFile));
            var startEvent = model.Events.First(evt =>
                evt.Type == "startEvent"
                && evt.SubprocessId is null
                && (string.IsNullOrWhiteSpace(evt.ProcessId)
                    || evt.ProcessId == model.ProcessId));
            var result = engine.ExecuteFromStartEvent(model, startEvent.Id, variables);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, $"No trace produced for {Path.GetFileName(bpmnFile)}");
            Assert.Contains(result, entry => entry.StartsWith("StartEvent:", StringComparison.Ordinal));

            // Export: Serialize model back to BPMN XML
            var xmlExported = parser.Serialize(model);
            Assert.False(string.IsNullOrWhiteSpace(xmlExported), $"Exported BPMN XML is empty for {Path.GetFileName(bpmnFile)}");

            // Roundtrip: Parse exported XML and compare structure
            var modelRoundtrip = await parser.ParseAsync(xmlExported, TestContext.Current.CancellationToken);
            Assert.NotNull(modelRoundtrip);
            Assert.NotNull(model.Activities);
            Assert.NotNull(modelRoundtrip.Activities);
            // Optionally: Compare key model properties for equality
            Assert.Equal(model.ProcessId, modelRoundtrip.ProcessId);
            Assert.Equal(model.Activities.Count(), modelRoundtrip.Activities.Count());
            Assert.Equal(model.Gateways.Count, modelRoundtrip.Gateways.Count);
            // Optionally: Compare XMLs (ignoring whitespace/ordering)
             //Assert.True(XmlEquals(xml, xmlExported), $"Roundtrip XML mismatch for {Path.GetFileName(bpmnFile)}");
        }

        private static IReadOnlyDictionary<string, object> RuntimeInputsFor(string fileName) =>
            fileName switch
            {
                "C.1.0.bpmn" or "C.1.1.bpmn" => new Dictionary<string, object>
                {
                    ["approved"] = true,
                    ["clarified"] = "yes"
                },
                _ => new Dictionary<string, object>()
            };

        private bool XmlEquals(string xml, string xmlExported)
        {
            return string.Equals(xml.Trim(), xmlExported.Trim(), StringComparison.Ordinal);
        }
    }
}
