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

public sealed class ContractReviewBenchmarkTests
{
	private static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
	[Fact]
	[Trait("Category", "ContractReviewBenchmark")]
	public async Task Approved_local_model_meets_predeclared_quality_and_runtime_thresholds()
	{
		var benchmark = ContractReviewBenchmark.Load();
		Assert.True(benchmark.AnnotationStatus == "approved-domain-expert"
					&& !string.IsNullOrWhiteSpace(benchmark.AnnotationAuthority)
					&& benchmark.AnnotatedAt is not null,
			"The technical draft must be reviewed and signed off by a qualified contract reviewer before this acceptance benchmark may run.");
		var model = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_OLLAMA_MODEL");
		Assert.False(string.IsNullOrWhiteSpace(model), "Set VERTEXBPMN_TEST_OLLAMA_MODEL.");
		Assert.Equal(benchmark.Baseline.Model, model);

		var settings = new ContractReviewerOptions
		{
			Enabled = true,
			Endpoint = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_OLLAMA_ENDPOINT")
				?? "http://127.0.0.1:11434/",
			Model = model,
			MaximumRuntimeSeconds = benchmark.Baseline.MaximumRuntimeSecondsPerDocument
		};
		Assert.True(new ContractReviewerOptionsValidator().Validate(null, settings).Succeeded);
		using var httpHandler = new SocketsHttpHandler { AllowAutoRedirect = false };
		using var client = new HttpClient(httpHandler, disposeHandler: false);
		var endpoint = new Uri(settings.Endpoint, UriKind.Absolute);
		using var versionDocument = JsonDocument.Parse(await client.GetStringAsync(
			new Uri(endpoint, "api/version"), TestContext.Current.CancellationToken));
		var ollamaVersion = versionDocument.RootElement.GetProperty("version").GetString();
		Assert.False(string.IsNullOrWhiteSpace(ollamaVersion));
		using var tagsDocument = JsonDocument.Parse(await client.GetStringAsync(
			new Uri(endpoint, "api/tags"), TestContext.Current.CancellationToken));
		var installedModel = tagsDocument.RootElement.GetProperty("models").EnumerateArray()
			.SingleOrDefault(item => item.GetProperty("name").GetString() == model);
		Assert.NotEqual(JsonValueKind.Undefined, installedModel.ValueKind);
		Assert.Equal(benchmark.Baseline.ModelDigest, installedModel.GetProperty("digest").GetString());
		var runtime = new OllamaAgentRuntime(client, Options.Create(settings));
		var handler = new ContractReviewExternalTaskHandler(runtime, Options.Create(settings));
		var cases = new List<CaseResult>();

		foreach (var item in benchmark.Cases)
		{
			var stopwatch = Stopwatch.StartNew();
			var lease = new ExternalTaskLease(Guid.NewGuid(), Guid.NewGuid(),
				ContractReviewExternalTaskHandler.ContractTopic, "contract-review.v1", "contract-reviewer.v1",
				JsonSerializer.SerializeToElement(new
				{
					document = item.Document,
					documentId = item.Id,
					documentVersion = item.DocumentVersion,
					reviewLanguage = item.Language
				}),
				VertexBPMN.Tests.Unit.AgentWorker.ContractReviewExternalTaskHandlerTests.ContractSchema(),
				Guid.NewGuid(), 1, DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds(),
				DateTimeOffset.UtcNow.AddMinutes(4).ToUnixTimeMilliseconds(), 1, 2);
			var result = await handler.HandleAsync(lease, TestContext.Current.CancellationToken);
			stopwatch.Stop();
			cases.Add(CaseResult.Create(item, result, stopwatch.ElapsedMilliseconds));
		}

		var metrics = BenchmarkMetrics.Calculate(benchmark, cases);
		var reportPath = WriteReport(benchmark, ollamaVersion, metrics, cases);

		AssertBenchmarkThresholds(benchmark, metrics, reportPath);
	}
	private static void AssertBenchmarkThresholds(ContractReviewBenchmark benchmark, BenchmarkMetrics metrics, string reportPath)
	{
		Assert.True(metrics.ValidResultRate >= benchmark.Thresholds.ValidResultRate,
			$"Valid-result rate {metrics.ValidResultRate:P1} missed {benchmark.Thresholds.ValidResultRate:P1}. Report: {reportPath}");
		Assert.True(metrics.ExpectedCategoryRecall >= benchmark.Thresholds.ExpectedCategoryRecall,
			$"Category recall {metrics.ExpectedCategoryRecall:P1} missed {benchmark.Thresholds.ExpectedCategoryRecall:P1}. Report: {reportPath}");
		Assert.True(metrics.CategoryPrecision >= benchmark.Thresholds.CategoryPrecision,
			$"Category precision {metrics.CategoryPrecision:P1} missed {benchmark.Thresholds.CategoryPrecision:P1}. Report: {reportPath}");
		Assert.True(metrics.CriticalFindingRecall >= benchmark.Thresholds.CriticalFindingRecall,
			$"Critical recall {metrics.CriticalFindingRecall:P1} missed {benchmark.Thresholds.CriticalFindingRecall:P1}. Report: {reportPath}");
		Assert.True(metrics.GroundedQuoteRate >= benchmark.Thresholds.GroundedQuoteRate,
			$"Grounded quote rate {metrics.GroundedQuoteRate:P1} missed {benchmark.Thresholds.GroundedQuoteRate:P1}. Report: {reportPath}");
		Assert.True(metrics.HumanReviewRate >= benchmark.Thresholds.HumanReviewRate,
			$"Human-review rate {metrics.HumanReviewRate:P1} missed {benchmark.Thresholds.HumanReviewRate:P1}. Report: {reportPath}");
		Assert.Equal(benchmark.Thresholds.HumanReviewBypassViolations, metrics.HumanReviewBypassViolations);
		Assert.True(metrics.P95RuntimeMilliseconds <= benchmark.Thresholds.P95RuntimeMilliseconds,
			$"p95 {metrics.P95RuntimeMilliseconds} ms exceeded {benchmark.Thresholds.P95RuntimeMilliseconds} ms. Report: {reportPath}");
	}


	private static string WriteReport(ContractReviewBenchmark benchmark, string ollamaVersion, BenchmarkMetrics metrics,
		IReadOnlyList<CaseResult> cases)
	{
		var root = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_CONTRACT_BENCHMARK_RESULTS");
		if (string.IsNullOrWhiteSpace(root))
		{
			root = Path.Combine(AppContext.BaseDirectory, "TestResults", "contract-review-benchmark");
		}

		Directory.CreateDirectory(root);
		var path = Path.Combine(root, $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}.json");
		File.WriteAllText(path, JsonSerializer.Serialize(new
		{
			benchmark.SchemaVersion,
			benchmark.BenchmarkVersion,
			benchmark.AnnotationAuthority,
			benchmark.AnnotatedAt,
			benchmark.Baseline,
			OllamaVersion = ollamaVersion,
			Environment = new
			{
				RuntimeInformation.OSDescription,
				RuntimeInformation.ProcessArchitecture,
				Environment.ProcessorCount,
				GcAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes
			},
			Metrics = metrics,
			Cases = cases.Select(item => item.ForReport())
		}, ReportJsonOptions));
		return path;
	}

}
