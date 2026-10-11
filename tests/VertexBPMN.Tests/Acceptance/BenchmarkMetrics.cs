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

internal sealed record BenchmarkMetrics(double ValidResultRate, double ExpectedCategoryRecall,
	double CategoryPrecision, double CriticalFindingRecall, double GroundedQuoteRate,
	double HumanReviewRate, int HumanReviewBypassViolations, long P95RuntimeMilliseconds)
{
	public static BenchmarkMetrics Calculate(ContractReviewBenchmark benchmark, IReadOnlyList<CaseResult> results)
	{
		var expected = benchmark.Cases.SelectMany(item => item.ExpectedFindings
			.Select(finding => (item.Id, finding.Category))).Distinct().ToHashSet();
		var actual = results.SelectMany(item => item.Findings.Select(finding => (item.Id, finding.Category)))
			.Distinct().ToHashSet();
		var truePositive = expected.Intersect(actual).Count();
		var critical = benchmark.Cases.SelectMany(item => item.ExpectedFindings.Where(finding => finding.Critical)
			.Select(finding => (Case: item, Finding: finding))).ToArray();
		var criticalMatches = critical.Count(expectedFinding => results.Single(result => result.Id == expectedFinding.Case.Id)
			.Findings.Any(actualFinding => actualFinding.Category == expectedFinding.Finding.Category
				&& (expectedFinding.Finding.Quote.Contains(actualFinding.Quote, StringComparison.Ordinal)
					|| actualFinding.Quote.Contains(expectedFinding.Finding.Quote, StringComparison.Ordinal))));
		var allFindings = results.SelectMany(result => result.Findings.Select(finding => (Result: result, Finding: finding))).ToArray();
		var grounded = allFindings.Count(value => benchmark.Cases.Single(item => item.Id == value.Result.Id)
			.Document.Contains(value.Finding.Quote, StringComparison.Ordinal));
		var sortedRuntime = results.Select(item => item.RuntimeMilliseconds).Order().ToArray();
		var p95Index = Math.Max(0, (int)Math.Ceiling(sortedRuntime.Length * 0.95) - 1);
		return new(
			Ratio(results.Count(item => item.Valid), results.Count),
			Ratio(truePositive, expected.Count),
			Ratio(truePositive, actual.Count),
			Ratio(criticalMatches, critical.Length),
			Ratio(grounded, allFindings.Length),
			Ratio(results.Count(item => item.Valid && item.RequiresHumanReview), results.Count),
			results.Count(item => item.HumanReviewBypassViolation),
			sortedRuntime[p95Index]);
	}

	private static double Ratio(int numerator, int denominator) => denominator == 0 ? 1 : (double)numerator / denominator;
}
