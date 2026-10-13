using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Infrastructure;

public sealed class SharedSqliteDbFixtureIsolationTests
{
	[Fact]
	public async Task Context_can_query_shared_data_while_another_context_has_an_active_reader()
	{
		await using var fixture = new SharedSqliteDbFixture();
		await fixture.InitializeAsync();
		var cancellation = TestContext.Current.CancellationToken;
		await using var first = new BpmnDbContext(fixture.BpmnOptions);
		await first.Database.OpenConnectionAsync(cancellation);
		await using var command = first.Database.GetDbConnection().CreateCommand();
		command.CommandText = "SELECT Id FROM ProcessDefinitions";
		await using var reader = await command.ExecuteReaderAsync(cancellation);
		Assert.True(await reader.ReadAsync(cancellation));

		await using var second = new BpmnDbContext(fixture.BpmnOptions);
		Assert.True(await second.ProcessDefinitions.AnyAsync(cancellation));
		Assert.NotSame(first.Database.GetDbConnection(), second.Database.GetDbConnection());
	}

	[Fact]
	public async Task Separate_collection_fixtures_cannot_read_each_others_database()
	{
		await using var first = new SharedSqliteDbFixture();
		await using var second = new SharedSqliteDbFixture();
		var cancellation = TestContext.Current.CancellationToken;
		await first.Connection.OpenAsync(cancellation);
		await second.Connection.OpenAsync(cancellation);
		await using var create = first.Connection.CreateCommand();
		create.CommandText = "CREATE TABLE FixtureIsolationProbe (Id INTEGER PRIMARY KEY)";
		await create.ExecuteNonQueryAsync(cancellation);
		await using var query = second.Connection.CreateCommand();
		query.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'FixtureIsolationProbe'";
		Assert.Equal(0L, await query.ExecuteScalarAsync(cancellation));
	}
}
