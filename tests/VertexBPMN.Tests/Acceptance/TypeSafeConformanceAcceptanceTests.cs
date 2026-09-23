using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Infrastructure.Messaging;
using Xunit;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// Live-Akzeptanz gegen die echte TypeSafe System One API (P3 Envelope-Konformität).
/// Filtered via Category=ContractReviewTypeSafe; ohne TYPESAFE_API_KEY automatisch übersprungen.
/// </summary>
public sealed class TypeSafeConformanceAcceptanceTests
{
    [Fact]
    [Trait("Category", "ContractReviewTypeSafe")]
    public async Task Live_TypeSafe_Judges_Conforming_ServiceTaskDispatch()
    {
        var key = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(key),
            "Set TYPESAFE_API_KEY to enable the live TypeSafe conformance acceptance test.");

        var options = new TypeSafeConformanceOptions { Enabled = true, ApiKey = key! };
        using var client = new TypeSafeConformanceClient(null, options,
            NullLogger<TypeSafeConformanceClient>.Instance);

        var state = JsonSerializer.Serialize(new
        {
            eventType = "ServiceTaskDispatch",
            processInstanceId = Guid.NewGuid().ToString(),
            tenantId = "tenant-a",
            declaredContract = "Pflicht: targetWorkerId (string), implementation (string); optional: attributes, variables.",
            payload = new { targetWorkerId = "worker-1", implementation = "calculateScore" }
        });

        var answer = await client.AskConformanceAsync(state, TestContext.Current.CancellationToken);
        Assert.NotNull(answer);
        Assert.Equal("conforms", answer!.Choice);
        Assert.True(answer.Confidence >= 0.5, $"Expected confident conforms, got {answer.Choice} @ {answer.Confidence:0.00}.");
    }

    [Fact]
    [Trait("Category", "ContractReviewTypeSafe")]
    public async Task Live_TypeSafe_Detects_Malformed_ServiceTaskDispatch()
    {
        var key = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(key),
            "Set TYPESAFE_API_KEY to enable the live TypeSafe conformance acceptance test.");

        var options = new TypeSafeConformanceOptions { Enabled = true, ApiKey = key! };
        using var client = new TypeSafeConformanceClient(null, options,
            NullLogger<TypeSafeConformanceClient>.Instance);

        var state = JsonSerializer.Serialize(new
        {
            eventType = "ServiceTaskDispatch",
            processInstanceId = Guid.NewGuid().ToString(),
            tenantId = "tenant-a",
            declaredContract = "Pflicht: targetWorkerId (string), implementation (string); optional: attributes, variables.",
            payload = new { implementation = "calculateScore" } // targetWorkerId fehlt
        });

        var answer = await client.AskConformanceAsync(state, TestContext.Current.CancellationToken);
        Assert.NotNull(answer);
        Assert.Equal("malformed", answer!.Choice);
    }
}
