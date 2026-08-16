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

    private static ExecutionToken CreateToken()
    {
        return new ExecutionToken(
            id: Guid.NewGuid(),
            processInstanceId: Guid.NewGuid(),
            currentNodeId: "node-1",
            nodeType: "task",
            variables: new Dictionary<string, object>(),
            createdAt: DateTime.UtcNow);
    }
}