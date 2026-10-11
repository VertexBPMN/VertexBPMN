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

public sealed class ContractReviewBenchmarkManifestTests
{
	private static readonly string[] AllowedAnnotationStatuses = ["pending-domain-expert-review", "approved-domain-expert"];
	[Fact]
	public void Versioned_manifest_has_complete_and_hash_bound_technical_annotations()
	{
		var benchmark = ContractReviewBenchmark.Load();

		Assert.Equal("vertex.contract-review-benchmark.v1", benchmark.SchemaVersion);
		Assert.Equal(20, benchmark.Cases.Count);
		Assert.Equal(20, benchmark.Cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
		Assert.Contains(benchmark.AnnotationStatus,
			AllowedAnnotationStatuses, StringComparer.Ordinal);
		Assert.All(benchmark.Cases, item =>
		{
			var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Document)))
				.ToLowerInvariant();
			Assert.Equal($"sha256:{hash}", item.DocumentVersion);
			Assert.NotEmpty(item.ExpectedFindings);
			Assert.All(item.ExpectedFindings, finding =>
			{
				Assert.Contains(finding.Category, ContractReviewBenchmark.AllowedCategories, StringComparer.Ordinal);
				Assert.Contains(finding.Quote, item.Document, StringComparison.Ordinal);
			});
		});
		Assert.InRange(benchmark.Thresholds.ValidResultRate, 0.01, 1.0);
		Assert.InRange(benchmark.Thresholds.ExpectedCategoryRecall, 0.01, 1.0);
		Assert.InRange(benchmark.Thresholds.CategoryPrecision, 0.01, 1.0);
		Assert.Equal(1.0, benchmark.Thresholds.CriticalFindingRecall);
		Assert.Equal(1.0, benchmark.Thresholds.GroundedQuoteRate);
		Assert.Equal(1.0, benchmark.Thresholds.HumanReviewRate);
		Assert.Equal(0, benchmark.Thresholds.HumanReviewBypassViolations);

		var processPath = Path.Combine(AppContext.BaseDirectory, "TestData", "ContractReviewBenchmark", "v1",
			"contract-review-process.bpmn");
		var process = XDocument.Load(processPath);
		XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
		XNamespace vertex = "https://vertexbpmn.io/schema/bpmn/1.0";
		var externalTask = Assert.Single(process.Descendants(vertex + "externalTask"));
		Assert.Equal("agent.contract-review", externalTask.Attribute("topic")?.Value);
		Assert.Equal("contract-reviewer.v1", externalTask.Attribute("agentProfileRef")?.Value);
		Assert.Single(process.Descendants(vertex + "output"));
		Assert.Equal(2, process.Descendants(bpmn + "userTask").Count());
		Assert.Single(process.Descendants(bpmn + "boundaryEvent"));
	}
}
