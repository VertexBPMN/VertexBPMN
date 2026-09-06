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
    /// BPMN-2.0-Conformance-Suite: fuehrt ALLE MIWG-2025-Referenzmodelle aus und
    /// vergleicht das reale Ergebnis mit der dokumentierten Baseline (MiwgBaseline).
    /// Gruen = Baseline eingehalten. Abweichung in beide Richtungen schlaegt fehl.
    /// </summary>
    public class MIWGTestSuite2025
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
        public async Task Engine_Should_Execute_MIWG_Bpmn_File(string bpmnFile)
        {
            var name = Path.GetFileName(bpmnFile);
            MiwgBaseline.Entry baseline = MiwgBaseline.Files[name];

            var (completed, detail) = await ExecuteAsync(bpmnFile);

            if (baseline.Completed)
            {
                Assert.True(completed,
                    $"[{name}] REGRESSION: Modell war Completed laut Baseline, scheitert jetzt aber: {detail}");
            }
            else
            {
                Assert.False(completed,
                    $"[{name}] CONFORMANCE-IMPROVED: Modell laeuft jetzt durch. Baseline auf Completed=true setzen und Feature dokumentieren. Detail: {detail}");
                Console.WriteLine($"[{name}] PENDING (dokumentiert): {detail}");
            }
        }

        internal static async Task<(bool Completed, string Detail)> ExecuteAsync(string bpmnFile)
        {
            try
            {
                var xml = File.ReadAllText(bpmnFile);
                var logger = new Mock<ILogger<BpmnParser>>();
                var parser = new BpmnParser(logger.Object, TracerProvider.Default);
                var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
                var engine = new ProcessEngine();
                var result = engine.Execute(model);
                if (result != null && result.Count > 0)
                    return (true, "TRACE_OK");
                return (false, "NO_TRACE (Parser/Engine lief, aber keine Execution-Trace erzeugt).");
            }
            catch (Exception ex)
            {
                return (false, $"{ex.GetType().Name}: {FirstLine(ex.Message)}");
            }
        }

        internal static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var i = s.IndexOf('\n');
            return i < 0 ? s : s.Substring(0, i);
        }
    }
}
