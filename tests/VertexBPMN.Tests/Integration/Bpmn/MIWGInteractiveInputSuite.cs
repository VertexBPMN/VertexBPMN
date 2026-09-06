using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Integration.Bpmn
{
    /// <summary>
    /// Interaktive MIWG-Modelle (User-Task-/DMN-getrieben): Die BPMN-Struktur ist konform,
    /// aber die Modelle benoetigen zur Laufzeit Eingaben, die ein echter Runtime von
    /// User-Task-Outputs bzw. DMN-Decision-Ergebnissen bezieht. Diese Suite versorgt die
    /// dokumentierten Eingaben und weist nach, dass die Engine die Modelle vollstaendig
    /// (bis zum End-Event) ausfuehren kann.
    ///
    /// Diese Suite ist ERGAENZEND zu MIWGTestSuite2025: Dort bleiben die Modelle in der
    /// ungetriebenen (eingabelosen) Ausfuehrung korrekt als "Pending (interaktiv)" dokumentiert.
    /// </summary>
    public class MIWGInteractiveInputSuite
    {
        private static readonly (string File, string Name, object[] Variables)[] Cases =
        {
            new("C.1.1.bpmn", "invoice approval (user-task outputs 'approved'/'clarified')",
                new object[] { new Dictionary<string, object> { ["approved"] = true, ["clarified"] = "yes" } }),
            new("C.8.0.bpmn", "DMN-driven vacation approval ('Vacation Approval')",
                new object[] { new Dictionary<string, object> { ["Vacation Approval"] = "Approved" } }),
            new("C.8.1.bpmn", "DMN-driven vacation approval ('Vacation Approval')",
                new object[] { new Dictionary<string, object> { ["Vacation Approval"] = "Approved" } }),
            new("C.9.0.bpmn", "DMN risk-decide (business-rule output 'riskLevels')",
                new object[] { new Dictionary<string, object> { ["riskLevels"] = new List<object> { "red" }, ["approved"] = true } }),
        };

        public static IEnumerable<object[]> GetCases()
        {
            foreach (var c in Cases)
                yield return new object[] { c.File, c.Name, c.Variables };
        }

        [Theory]
        [MemberData(nameof(GetCases))]
        public async Task Engine_Executes_Interactive_MIWG_Model_To_Completion(string file, string name, object[] variables)
        {
            var vars = (IReadOnlyDictionary<string, object>)variables[0];
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference");
            var path = Path.Combine(dir, file);
            Assert.True(File.Exists(path), $"Reference file not found: {path}");

            var xml = File.ReadAllText(path);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

            var engine = new ProcessEngine();
            var result = engine.Execute(model, vars);

            Assert.NotNull(result);
            Assert.True(result.Count > 0, $"[{file}] No trace produced with documented inputs ({name}).");
            Assert.Contains(result, line =>
                line.Contains("EndEvent", StringComparison.OrdinalIgnoreCase)
                || line.Contains("ProcessCompleted", StringComparison.OrdinalIgnoreCase));
        }
    }
}
