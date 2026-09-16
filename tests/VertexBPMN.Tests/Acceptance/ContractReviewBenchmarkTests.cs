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
    [Fact]
    public void Versioned_manifest_has_complete_and_hash_bound_technical_annotations()
    {
        var benchmark = ContractReviewBenchmark.Load();

        Assert.Equal("vertex.contract-review-benchmark.v1", benchmark.SchemaVersion);
        Assert.Equal(20, benchmark.Cases.Count);
        Assert.Equal(20, benchmark.Cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(benchmark.AnnotationStatus,
            new[] { "pending-domain-expert-review", "approved-domain-expert" });
        Assert.All(benchmark.Cases, item =>
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Document)))
                .ToLowerInvariant();
            Assert.Equal($"sha256:{hash}", item.DocumentVersion);
            Assert.NotEmpty(item.ExpectedFindings);
            Assert.All(item.ExpectedFindings, finding =>
            {
                Assert.Contains(finding.Category, ContractReviewBenchmark.AllowedCategories);
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

public sealed class ContractReviewBenchmarkTests
{
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
            Model = model!,
            MaximumRuntimeSeconds = benchmark.Baseline.MaximumRuntimeSecondsPerDocument
        };
        Assert.True(new ContractReviewerOptionsValidator().Validate(null, settings).Succeeded);
        using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
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
        var reportPath = WriteReport(benchmark, ollamaVersion!, metrics, cases);

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
            root = Path.Combine(AppContext.BaseDirectory, "TestResults", "contract-review-benchmark");
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
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        return path;
    }

}

internal sealed record CaseResult(string Id, bool Valid, string? FailureCode, long RuntimeMilliseconds,
    bool RequiresHumanReview, IReadOnlyList<ActualFinding> Findings, bool HumanReviewBypassViolation)
{
    public static CaseResult Create(BenchmarkCase item, ExternalTaskHandlerResult result, long runtime)
    {
        if (result.Outcome != ExternalTaskHandlerOutcome.Success)
            return new(item.Id, false, result.Code, runtime, false, [], false);
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
        Id, Valid, FailureCode, RuntimeMilliseconds, RequiresHumanReview,
        Findings = Findings.Select(item => new { item.Category, item.SectionId, item.StartLine, item.EndLine, item.Quote }),
        HumanReviewBypassViolation
    };
}

internal sealed record ActualFinding(string Category, string SectionId, int StartLine, int EndLine, string Quote);

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

internal sealed record BenchmarkBaseline(string Runtime, string Model, string ModelDigest,
    string PromptVersion, string ResultSchemaVersion, double Temperature, bool Thinking,
    int MaximumRuntimeSecondsPerDocument);
internal sealed record BenchmarkThresholds(double ValidResultRate, double ExpectedCategoryRecall,
    double CategoryPrecision, double CriticalFindingRecall, double GroundedQuoteRate,
    double HumanReviewRate, int HumanReviewBypassViolations, long P95RuntimeMilliseconds);
internal sealed record BenchmarkCase(string Id, string Language, IReadOnlyList<string> Tags,
    string DocumentVersion, string Document, IReadOnlyList<ExpectedFinding> ExpectedFindings);
internal sealed record ExpectedFinding(string Category, string Quote, bool Critical);
