using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Successful_read_result_survives_a_new_store_and_context()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		Assert.Equal(SourceControlOperationState.Succeeded, (await harness.RunAsync((_, _) => Task.CompletedTask, Token)).State);
		await using var restarted = fixture.NewContext();
		Assert.Equal(ReadRevision.Value, await fixture.NewStore(restarted).ReadHttpsResultAsync<string>(Context, harness.Id,
			Admin, ExecutionRegistry(() => HttpsPreparationOptions(fixture)), Token));
	}

	[Theory]
	[InlineData("ciphertext")]
	[InlineData("fence")]
	[InlineData("wrong-type")]
	public async Task Stored_read_results_cannot_be_tampered_or_reinterpreted(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		await harness.RunAsync((_, _) => Task.CompletedTask, Token);
		if (change == "ciphertext")
		{
			await fixture.Db.SourceControlOperations.Where(row => row.Id == harness.Id)
				.ExecuteUpdateAsync(update => update.SetProperty(row => row.ProtectedResult, "invalid-ciphertext"), Token);
		}
		else if (change == "fence")
		{
			await fixture.Db.SourceControlOperations.Where(row => row.Id == harness.Id)
				.ExecuteUpdateAsync(update => update.SetProperty(row => row.Fence, 2), Token);
		}
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, async () =>
		{
			if (change == "wrong-type")
			{
				await fixture.Store.ReadHttpsResultAsync<ModelDiff>(Context, harness.Id, Admin, registry, Token);
			}
			else
			{
				await fixture.Store.ReadHttpsResultAsync<string>(Context, harness.Id, Admin, registry, Token);
			}
		});
	}

	[Fact]
	public async Task Durable_read_result_rechecks_roles_and_does_not_serve_a_foreign_actor()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		await harness.RunAsync((_, _) => Task.CompletedTask, Token);
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		await RejectAsync(SourceControlErrorCode.NotFound, () => fixture.Store.ReadHttpsResultAsync<string>(Context, harness.Id, [], registry, Token));
		await RejectAsync(SourceControlErrorCode.NotFound, () => fixture.Store.ReadHttpsResultAsync<string>(
			new(Context.TenantId, "other-actor"), harness.Id, Admin, registry, Token));
	}

	[Fact]
	public async Task Snapshot_result_roundtrips_exact_original_bytes_and_generation()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		var content = System.Text.Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"/>");
		var expected = new ModelSnapshot("models/process.bpmn", SourceModelKind.Bpmn, Guid.NewGuid(), 3, content);
		var outcome = await harness.Runner.RunControlledAsync(Context, harness.Id, "runner", 1,
			(_, _) => Task.FromResult(expected), TimeSpan.FromSeconds(30), Token);
		Assert.Equal(SourceControlOperationState.Succeeded, outcome.State);
		await using var restarted = fixture.NewContext();
		var actual = await fixture.NewStore(restarted).ReadHttpsResultAsync<ModelSnapshot>(Context, harness.Id, Admin,
			ExecutionRegistry(() => HttpsPreparationOptions(fixture)), Token);
		Assert.Equal(expected.ContentSha256, actual.ContentSha256);
		Assert.Equal(expected.DocumentGeneration, actual.DocumentGeneration);
		Assert.Equal(expected.LocalRevision, actual.LocalRevision);
		Assert.Equal(content, actual.CopyContent());
	}
}
