namespace VertexBPMN.Tests.Integration.Engine;

using Shouldly;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence.InMemory;
using Xunit;

public sealed class ExecutionTokenStateTests
{
    [Fact]
    public void NewExecutionToken_HasPendingState()
    {
        var token = CreateToken();

        token.State.ShouldBe(
            ExecutionToken.PendingState);
    }

    [Fact]
    public void SetState_ChangesTokenState()
    {
        var token = CreateToken();

        token.SetState(
            ExecutionToken.CompletedState);

        token.State.ShouldBe(
            ExecutionToken.CompletedState);
    }

    [Fact]
    public void SetState_RejectsEmptyState()
    {
        var token = CreateToken();

        Should.Throw<ArgumentException>(
            () => token.SetState(" "));
    }

    [Fact]
    public async Task InMemoryStore_NormalizesLegacyTokenWithoutState()
    {
        var store = new InMemoryProcessInstanceStore();
        var token = CreateToken();

        token.State = null;

        await store.SaveTokenAsync(token);

        var persisted = await store.GetTokenAsync(token.Id);

        persisted.State.ShouldBe(
            ExecutionToken.PendingState);
    }

    [Fact]
    public async Task InMemoryStore_ReturnsOnlyPendingTokens()
    {
        var store = new InMemoryProcessInstanceStore();

        var pending = CreateToken();
        pending.AssignedWorker = "worker-1";
        pending.SetState(ExecutionToken.PendingState);

        var completed = CreateToken();
        completed.SetState(ExecutionToken.CompletedState);

        var failed = CreateToken();
        failed.SetState(ExecutionToken.FailedState);

        await store.SaveTokenAsync(pending);
        await store.SaveTokenAsync(completed);
        await store.SaveTokenAsync(failed);

        var result =
            await store.GetPendingTokensAsync();

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(pending.Id);
    }

    [Fact]
    public async Task InMemoryStore_DoesNotUseAssignedWorkerAsPendingCriterion()
    {
        var store = new InMemoryProcessInstanceStore();

        var completed = CreateToken();
        completed.AssignedWorker = null;
        completed.SetState(ExecutionToken.CompletedState);

        await store.SaveTokenAsync(completed);

        var result =
            await store.GetPendingTokensAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task WaitingMessage_DoesNotRemainPending()
    {
        var store = new InMemoryProcessInstanceStore();
        var token = CreateToken();

        token.SetState(
            ExecutionToken.WaitingState);

        await store.SaveTokenAsync(token);

        var pending =
            await store.GetPendingTokensAsync();

        pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompletedToken_IsNotPending()
    {
        var store = new InMemoryProcessInstanceStore();
        var token = CreateToken();

        token.SetState(
            ExecutionToken.CompletedState);

        await store.SaveTokenAsync(token);

        var pending =
            await store.GetPendingTokensAsync();

        pending.ShouldBeEmpty();
    }

    [Fact]
    public void MatchingCorrelation_ReturnsTrue()
    {
        var token = CreateToken();

        token.Variables["orderId"] = "ORD-42";

        var message = new Message(
            "order-approved",
            "message-key",
            new Dictionary<string, object>
            {
                ["orderId"] = "ORD-42"
            });

        MatchesCorrelation(
                "orderId",
                token,
                message)
            .ShouldBeTrue();
    }

    [Fact]
    public void DifferentCorrelation_ReturnsFalse()
    {
        var token = CreateToken();

        token.Variables["orderId"] = "ORD-42";

        var message = new Message(
            "order-approved",
            "message-key",
            new Dictionary<string, object>
            {
                ["orderId"] = "ORD-99"
            });

        MatchesCorrelation(
                "orderId",
                token,
                message)
            .ShouldBeFalse();
    }
    [Fact]
    public void NewToken_IsPending()
    {
        var token = CreateToken();

        token.State.ShouldBe(
            ExecutionToken.PendingState);
    }

    [Fact]
    public async Task WaitingToken_IsNotPending()
    {
        var store = new InMemoryProcessInstanceStore();
        var token = CreateToken();

        token.SetState(
            ExecutionToken.WaitingState);

        await store.SaveTokenAsync(token);

        var pending =
            await store.GetPendingTokensAsync();

        pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveToken_PreservesResumeState()
    {
        var store = new InMemoryProcessInstanceStore();
        var token = CreateToken();

        token.CurrentNodeId = "message-catch";
        token.NodeType = "intermediateCatchEvent";
        token.Variables["orderId"] = "ORD-42";
        token.SetState(
            ExecutionToken.WaitingState);

        await store.SaveTokenAsync(token);

        var loaded =
            await store.GetTokenAsync(token.Id);

        loaded.CurrentNodeId.ShouldBe(
            "message-catch");

        loaded.NodeType.ShouldBe(
            "intermediateCatchEvent");

        loaded.Variables["orderId"]
            .ShouldBe("ORD-42");

        loaded.State.ShouldBe(
            ExecutionToken.WaitingState);
    }
    private static ExecutionToken CreateToken()
    {
        return new ExecutionToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "message-catch",
            "intermediateCatchEvent",
            new Dictionary<string, object>(),
            DateTime.UtcNow);
    }

    private static bool MatchesCorrelation(
        string? correlationKey,
        ExecutionToken token,
        Message message)
    {
        if (string.IsNullOrWhiteSpace(correlationKey))
        {
            return true;
        }

        if (!token.Variables.TryGetValue(
                correlationKey,
                out var expectedValue))
        {
            return false;
        }

        if (!message.Variables.TryGetValue(
                correlationKey,
                out var receivedValue))
        {
            return false;
        }

        return Equals(
            expectedValue,
            receivedValue);
    }
}