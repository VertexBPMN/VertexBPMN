using System.Text.Json;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class ExternalTaskNodeSemanticsTests
{
    [Fact]
    public void CompletionBeforeConfirmedBusinessEffectIsRejected()
    {
        var session = new ExternalTaskNodeSession(Lease());
        using var result = JsonDocument.Parse("{\"approved\":true}");

        var error = Assert.Throws<InvalidOperationException>(() => session.Complete(result.RootElement));

        Assert.Equal("business_effect_unconfirmed", error.Message);
    }

    [Fact]
    public void ConfirmedEffectAllowsStableCompletionReceipt()
    {
        var lease = Lease();
        var session = new ExternalTaskNodeSession(lease);
        using var result = JsonDocument.Parse("{\"approved\":true}");

        Assert.True(session.ConfirmBusinessEffect($"external-task:{lease.ActivityExecutionId:N}:primary"));
        var first = session.Complete(result.RootElement);
        var replay = session.Complete(result.RootElement);

        Assert.Equal(lease.LeaseId, first.LeaseId);
        Assert.Equal(lease.LeaseGeneration, first.LeaseGeneration);
        Assert.Equal(first.CompletionId, replay.CompletionId);
        Assert.NotEqual(Guid.Empty, first.CompletionId);
    }

    [Fact]
    public void LeaseLossRejectsLateCompletionAndHeartbeat()
    {
        var lease = Lease();
        var session = new ExternalTaskNodeSession(lease);
        Assert.True(session.ConfirmBusinessEffect(session.EffectKey));
        using var result = JsonDocument.Parse("{\"approved\":true}");

        session.LoseLease();

        Assert.Equal("lease_lost", Assert.Throws<InvalidOperationException>(() =>
            session.Complete(result.RootElement)).Message);
        Assert.Equal("lease_lost", Assert.Throws<InvalidOperationException>(() =>
            session.Heartbeat()).Message);
    }

    [Fact]
    public void BusinessFailRejectsCodeOutsideFrozenAllowlist()
    {
        var session = new ExternalTaskNodeSession(Lease());
        Assert.Equal("business_error_not_allowed", Assert.Throws<InvalidOperationException>(() =>
            session.Fail("business", "invented_error")).Message);
    }

    [Fact]
    public void ConfigurationRejectsWildcardBeforeClaimMapping()
    {
        var configuration = new ExternalTaskNodeConfiguration(new Uri("https://engine.example/"),
            new Uri("https://issuer.example/token"), "worker", "credential:worker", "a", ["*"], []);
        var worker = new ExternalTaskWorkerContext("issuer", "worker", "a",
            new HashSet<string>(["*"], StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal("worker_forbidden", Assert.Throws<InvalidOperationException>(() =>
            configuration.Claim(worker)).Message);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("topic")]
    [InlineData("profile")]
    public void ConfigurationRejectsClaimsOutsideConfiguredAuthority(string defect)
    {
        var configuration = new ExternalTaskNodeConfiguration(new Uri("https://engine.example/"),
            new Uri("https://issuer.example/token"), "worker", "credential:worker", "a", ["test.work"], ["p1"]);
        var worker = new ExternalTaskWorkerContext("issuer", "worker", defect == "tenant" ? "b" : "a",
            new HashSet<string>([defect == "topic" ? "other" : "test.work"], StringComparer.Ordinal),
            new HashSet<string>([defect == "profile" ? "other" : "p1"], StringComparer.Ordinal));

        Assert.Equal("worker_forbidden", Assert.Throws<InvalidOperationException>(() =>
            configuration.Claim(worker)).Message);
    }

    [Fact]
    public void FailureReplayPreservesReceiptIdentity()
    {
        var session = new ExternalTaskNodeSession(Lease());
        var first = session.Fail("business", "contract_rejected");
        var replay = session.Fail("business", "contract_rejected");
        Assert.Equal(first.FailureId, replay.FailureId);
    }

    [Fact]
    public void InputNodeRejectsStatusFromDifferentLeaseGeneration()
    {
        var lease = Lease();
        var status = new ExternalTaskStatus(lease.JobId, lease.ActivityExecutionId, lease.Topic, "Leased",
            lease.ContractVersion, lease.AgentProfileVersion, lease.Deadline, 2, 2, 2, lease.LeaseExpiresAt,
            lease.ApplicationReleaseId, lease.FlowRevision);
        var configuration = new ExternalTaskNodeConfiguration(new Uri("https://engine.example/"),
            new Uri("https://issuer.example/token"), "worker", "credential:worker", "a", ["test.work"], []);

        Assert.Equal("lease_lost", Assert.Throws<InvalidOperationException>(() =>
            configuration.Load(lease, status)).Message);
    }

    [Fact]
    public void ProcessStartIsSeparateActionWithTenantAndRoleAuthority()
    {
        var start = new ExternalTaskProcessStartNode("p", null, "synthetic", "a", "start-1");
        Assert.False(start.CanExecute("a", new HashSet<string>(["ExternalTaskWorker"], StringComparer.Ordinal)));
        Assert.False(start.CanExecute("b", new HashSet<string>(["ProcessManager"], StringComparer.Ordinal)));
        Assert.True(start.CanExecute("a", new HashSet<string>(["ProcessManager"], StringComparer.Ordinal)));
        Assert.Equal("api/runtime/start", ExternalTaskProcessStartNode.Route);
    }

    [Fact]
    public void ConfigurationRejectsEmbeddedBearerCredential()
    {
        var configuration = new ExternalTaskNodeConfiguration(new Uri("https://engine.example/"),
            new Uri("https://issuer.example/token"), "worker", "Bearer embedded-token", "a", ["test.work"], []);
        var worker = new ExternalTaskWorkerContext("issuer", "worker", "a",
            new HashSet<string>(["test.work"], StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
        Assert.Equal("credential_reference_required", Assert.Throws<InvalidOperationException>(() =>
            configuration.Claim(worker)).Message);
    }

    [Fact]
    public void DifferentBusinessEffectsHaveDistinctKeysWithinSameActivity()
    {
        var lease = Lease();
        var payment = new ExternalTaskNodeSession(lease, "payment");
        var notification = new ExternalTaskNodeSession(lease, "notification");
        Assert.NotEqual(payment.EffectKey, notification.EffectKey, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("p1,p2")]
    public void ConfigurationRejectsWildcardOrCommaSeparatedProfiles(string profile)
    {
        var configuration = new ExternalTaskNodeConfiguration(new Uri("https://engine.example/"),
            new Uri("https://issuer.example/token"), "worker", "credential:worker", "a", ["test.work"], [profile]);
        var worker = new ExternalTaskWorkerContext("issuer", "worker", "a",
            new HashSet<string>(["test.work"], StringComparer.Ordinal), new HashSet<string>([profile], StringComparer.Ordinal));
        Assert.Equal("worker_forbidden", Assert.Throws<InvalidOperationException>(() =>
            configuration.Claim(worker)).Message);
    }

    private static ExternalTaskLease Lease() => new(Guid.NewGuid(), Guid.NewGuid(), "test.work", "v1",
        null, JsonSerializer.SerializeToElement(new { text = "synthetic" }),
        "{\"businessErrors\":[\"contract_rejected\"]}", Guid.NewGuid(), 1,
        DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds(),
        DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds(), 1, 2, "rel-1", 1);
}
