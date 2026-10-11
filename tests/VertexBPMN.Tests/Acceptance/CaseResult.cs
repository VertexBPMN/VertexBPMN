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

internal sealed record CaseResult(string Id, bool Valid, string? FailureCode, long RuntimeMilliseconds,
	bool RequiresHumanReview, IReadOnlyList<ActualFinding> Findings, bool HumanReviewBypassViolation)
{
	public static CaseResult Create(BenchmarkCase item, ExternalTaskHandlerResult result, long runtime)
	{
		if (result.Outcome != ExternalTaskHandlerOutcome.Success)
		{
			return new(item.Id, false, result.Code, runtime, false, [], false);
		}

		try
		{
			var findings = JsonSerializer.Deserialize<ActualFinding[]>(result.Result.GetProperty("findings").GetString()!,
				ContractReviewBenchmark.JsonOptions) ?? [];
			var requiresHumanReview = result.Result.GetProperty("requiresHumanReview").GetBoolean();
			return new(item.Id, true, null, runtime, requiresHumanReview, findings,
				item.Tags.Contains("security", StringComparer.OrdinalIgnoreCase) && !requiresHumanReview);
		}
		catch (Exception exception) when (exception is JsonException or InvalidOperationException)
		{
			return new(item.Id, false, "benchmark_parse_failure", runtime, false, [], false);
		}
	}

	public object ForReport() => new
	{
		Id,
		Valid,
		FailureCode,
		RuntimeMilliseconds,
		RequiresHumanReview,
		Findings = Findings.Select(item => new { item.Category, item.SectionId, item.StartLine, item.EndLine, item.Quote }),
		HumanReviewBypassViolation
	};
}
