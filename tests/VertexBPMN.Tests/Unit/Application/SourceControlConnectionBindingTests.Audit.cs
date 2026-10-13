using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Infrastructure.SourceControl;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Completed_writes_export_once_and_recover_after_central_commit_before_ack()
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		await using var centralConnection = new SqliteConnection("Data Source=:memory:");
		await centralConnection.OpenAsync(Token);
		await using var central = new ProcessMiningEventDbContext(new DbContextOptionsBuilder<ProcessMiningEventDbContext>().UseSqlite(centralConnection).Options);
		await central.Database.MigrateAsync(Token);
		var exporter = new SourceControlAuditExporter(fixture.Db, central, setup.Store);
		Assert.Equal(2, await exporter.ExportAsync(Token));
		Assert.Equal(0, await exporter.ExportAsync(Token));
		var entries = await central.AuditLogs.AsNoTracking().ToArrayAsync(Token);
		Assert.Equal(2, entries.Length);
		Assert.All(entries, entry => { Assert.Equal(Context.TenantId, entry.TenantId); Assert.Equal(Context.ActorId, entry.UserId); Assert.NotNull(entry.SourceControlEventId); });
		Assert.All(entries, entry => Assert.DoesNotContain("definitions", entry.DetailsJson, StringComparison.Ordinal));
		await fixture.Db.SourceControlOperations.Where(row => row.Id == setup.Submission.PushOperationId)
			.ExecuteUpdateAsync(update => update.SetProperty(row => row.AuditPending, true), Token);
		Assert.Equal(1, await exporter.ExportAsync(Token));
		Assert.Equal(2, await central.AuditLogs.CountAsync(Token));
	}

	[Fact]
	public async Task Central_audit_failure_retains_durable_evidence_and_does_not_change_write_success()
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		await using var centralConnection = new SqliteConnection("Data Source=:memory:");
		await centralConnection.OpenAsync(Token);
		await using var central = new ProcessMiningEventDbContext(new DbContextOptionsBuilder<ProcessMiningEventDbContext>().UseSqlite(centralConnection).Options);
		// Deliberately omit central schema in this isolated in-memory database.
		Assert.Equal(0, await new SourceControlAuditExporter(fixture.Db, central, setup.Store).ExportAsync(Token));
		await setup.Store.PruneExpiredDetailsAsync(DateTimeOffset.UtcNow.AddDays(100), Token);
		var row = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(value => value.Id == setup.Submission.PushOperationId, Token);
		Assert.True(row.AuditPending);
		Assert.NotNull(row.AuditRetryAfterUtcTicks);
		Assert.NotEmpty(row.ProtectedRequest);
		Assert.Equal((int)VertexBPMN.SourceControl.Abstractions.SourceControlOperationState.Pushed, row.State);
	}
}
