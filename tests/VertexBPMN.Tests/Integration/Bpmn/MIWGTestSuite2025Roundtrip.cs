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

namespace VertexBPMN.Tests.Integration.Bpmn;

/// <summary>
/// Roundtrip-Conformance: fuehrt ALLE MIWG-2025-Referenzmodelle aus, exportiert das
/// Modell zurueck zu BPMN-XML und reproduziert es erneut (Parse -> Execute -> Serialize -> Reparse).
/// Ergebnis wird gegen dieselbe Baseline geprueft wie MiwgTestSuite2025.
/// </summary>
public class MiwgTestSuite2025Roundtrip
{
	public static IEnumerable<object[]> GetBpmnFiles()
	{
		var files = MiwgBaseline.ReferenceFiles().ToList();
		MiwgBaseline.EnsureCompleteCoverage(files);
		foreach (var file in files)
		{
			yield return new object[] { file };
		}
	}

	[Theory]
	[MemberData(nameof(GetBpmnFiles))]
	public async Task Engine_Should_Import_Export_Roundtrip_Bpmn_File(string bpmnFile)
	{
		var name = Path.GetFileName(bpmnFile);
		var baseline = MiwgBaseline.Files[name];

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
			var xml = await File.ReadAllTextAsync(bpmnFile, TestContext.Current.CancellationToken);
			var logger = new Mock<ILogger<BpmnParser>>();
			var parser = new BpmnParser(logger.Object, TracerProvider.Default);
			var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

			var engine = new ProcessEngine();
			var result = await engine.ExecuteAsync(model, TestContext.Current.CancellationToken);
			if (result == null || result.Count == 0)
			{
				return (false, "NO_TRACE");
			}

			var xmlExported = parser.Serialize(model);
			if (string.IsNullOrWhiteSpace(xmlExported))
			{
				return (false, "EMPTY_EXPORT");
			}

			var modelRoundtrip = await parser.ParseAsync(xmlExported, TestContext.Current.CancellationToken);
			Assert.NotNull(model.Gateways);
			Assert.NotNull(modelRoundtrip.Gateways);
			var ok = model.Activities != null
					  && modelRoundtrip?.Activities != null
					  && model.ProcessId == modelRoundtrip.ProcessId
					  && model.Activities.Count() == modelRoundtrip.Activities.Count()
					  && model.Gateways.Count == modelRoundtrip.Gateways.Count;
			return ok
				? (true, "ROUNDTRIP_OK")
				: (false, "ROUNDTRIP_MISMATCH (Modelleigenschaften differieren nach Export/Reimport).");
		}
		catch (Exception ex)
		{
			return (false, $"{ex.GetType().Name}: {MiwgTestSuite2025.FirstLine(ex.Message)}");
		}
	}
}
