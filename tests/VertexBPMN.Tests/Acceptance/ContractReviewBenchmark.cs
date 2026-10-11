using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using VertexBPMN.AgentWorker;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Acceptance;

internal sealed record ContractReviewBenchmark(string SchemaVersion, string BenchmarkVersion,
	string AnnotationStatus, string? AnnotationAuthority, DateTimeOffset? AnnotatedAt,
	BenchmarkBaseline Baseline, BenchmarkThresholds Thresholds, IReadOnlyList<BenchmarkCase> Cases)
{
	internal static readonly string[] AllowedCategories =
		["termination", "liability", "payment", "confidentiality", "data-protection", "compliance", "governing-law", "ambiguity", "other"];
	internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static ContractReviewBenchmark Load()
	{
		var path = Path.Combine(AppContext.BaseDirectory, "TestData", "ContractReviewBenchmark", "v1", "benchmark.json");
		return JsonSerializer.Deserialize<ContractReviewBenchmark>(File.ReadAllText(path), JsonOptions)
			?? throw new InvalidOperationException($"Benchmark manifest is empty: {path}");
	}
}
