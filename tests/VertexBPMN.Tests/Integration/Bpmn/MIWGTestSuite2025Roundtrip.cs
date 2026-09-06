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
    /// Roundtrip-Conformance: fuehrt ALLE MIWG-2025-Referenzmodelle aus, exportiert das
    /// Modell zurueck zu BPMN-XML und reproduziert es erneut (Parse -> Execute -> Serialize -> Reparse).
    /// Ergebnis wird gegen dieselbe Baseline geprueft wie MIWGTestSuite2025.
    /// </summary>
    public class MIWGTestSuite2025Roundtrip
    {
        public static IEnumerable<object[]> GetBpmnFiles()
        {
            var files = MiwgBaseline.ReferenceFiles().ToList();
            MiwgBaseline.EnsureCompleteCoverage(files);
            foreach (var file in files)
                yield return new object[] { file };
        }

        [Theory]
        [MemberData(nameof(GetBpmnFiles))]
        public async Task Engine_Should_Import_Export_Roundtrip_Bpmn_File(string bpmnFile)
        {
            var name = Path.GetFileName(bpmnFile);
            MiwgBaseline.Entry baseline = MiwgBaseline.Files[name];

            var (completed, detail) = await RoundtripAsync(bpmnFile);

            if (baseline.Completed)
            {
                Assert.True(completed,
                    $"[{name}] REGRESSION: Rundreise war Completed laut Baseline, scheitert jetzt aber: {detail}");
            }
            else
            {
                Assert.False(completed,
                    $"[{name}] CONFORMANCE-IMPROVED: Rundreise laeuft jetzt durch. Baseline auf Completed=true setzen und dokumentieren. Detail: {detail}");
                Console.WriteLine($"[{name}] PENDING (dokumentiert): {detail}");
            }
        }

        internal static async Task<(bool Completed, string Detail)> RoundtripAsync(string bpmnFile)
        {
            try
            {
                var xml = File.ReadAllText(bpmnFile);
                var logger = new Mock<ILogger<BpmnParser>>();
                var parser = new BpmnParser(logger.Object, TracerProvider.Default);
                var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

                var engine = new ProcessEngine();
                var result = engine.Execute(model);
                if (result == null || result.Count == 0)
                    return (false, "NO_TRACE");

                var xmlExported = parser.Serialize(model);
                if (string.IsNullOrWhiteSpace(xmlExported))
                    return (false, "EMPTY_EXPORT");

                var modelRoundtrip = await parser.ParseAsync(xmlExported, TestContext.Current.CancellationToken);
                bool ok = model.Activities != null
                          && modelRoundtrip != null
                          && modelRoundtrip.Activities != null
                          && model.ProcessId == modelRoundtrip.ProcessId
                          && model.Activities.Count() == modelRoundtrip.Activities.Count()
                          && model.Gateways.Count == modelRoundtrip.Gateways.Count;
                return ok
                    ? (true, "ROUNDTRIP_OK")
                    : (false, "ROUNDTRIP_MISMATCH (Modelleigenschaften differieren nach Export/Reimport).");
            }
            catch (Exception ex)
            {
                return (false, $"{ex.GetType().Name}: {MIWGTestSuite2025.FirstLine(ex.Message)}");
            }
        }
    }
}
