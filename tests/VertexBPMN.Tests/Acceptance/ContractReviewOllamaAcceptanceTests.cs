using System.Text.Json;
using Microsoft.Extensions.Options;
using VertexBPMN.AgentWorker;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Acceptance;

public sealed class ContractReviewOllamaAcceptanceTests
{
    [Fact]
    [Trait("Category", "ContractReviewOllama")]
    public async Task Local_model_produces_grounded_result_for_versioned_synthetic_contract()
    {
        var model = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_OLLAMA_MODEL");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(model),
            "Set VERTEXBPMN_TEST_OLLAMA_MODEL to an installed tool-capable Ollama model for local acceptance.");
        var settings = new ContractReviewerOptions
        {
            Enabled = true,
            Endpoint = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_OLLAMA_ENDPOINT")
                ?? "http://127.0.0.1:11434/",
            Model = model!,
            MaximumRuntimeSeconds = 180
        };
        Assert.True(new ContractReviewerOptionsValidator().Validate(null, settings).Succeeded);
        using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
        var runtime = new OllamaAgentRuntime(client, Options.Create(settings));
        var handler = new ContractReviewExternalTaskHandler(runtime, Options.Create(settings));
        var text = """
            # Liability
            Supplier liability is unlimited, including indirect damages.
            # Termination
            Customer may terminate with thirty days written notice.
            """;
        var lease = new ExternalTaskLease(Guid.NewGuid(), Guid.NewGuid(),
            ContractReviewExternalTaskHandler.ContractTopic, "contract-review.v1", "contract-reviewer.v1",
            JsonSerializer.SerializeToElement(new { document = text, documentId = "benchmark-001", documentVersion = "sha256:benchmark-001", reviewLanguage = "en" }),
            VertexBPMN.Tests.Unit.AgentWorker.ContractReviewExternalTaskHandlerTests.ContractSchema(),
            Guid.NewGuid(), 1, DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeMilliseconds(),
            DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds(), 1, 2);

        var result = await handler.HandleAsync(lease, TestContext.Current.CancellationToken);

        Assert.True(result.Outcome == ExternalTaskHandlerOutcome.Success,
            $"Expected a successful review, but the handler returned {result.Outcome} ({result.Code ?? "no-code"}).");
        Assert.True(result.Result.GetProperty("requiresHumanReview").GetBoolean());
        Assert.Equal("sha256:benchmark-001", result.Result.GetProperty("documentVersion").GetString());
        var findings = JsonSerializer.Deserialize<JsonElement>(result.Result.GetProperty("findings").GetString()!);
        Assert.Contains(findings.EnumerateArray(), finding =>
            finding.GetProperty("category").GetString() == "liability"
            && finding.GetProperty("quote").GetString()!.Contains("liability is unlimited", StringComparison.OrdinalIgnoreCase));
    }

}
