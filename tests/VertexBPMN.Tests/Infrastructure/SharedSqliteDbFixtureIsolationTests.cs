namespace VertexBPMN.Tests.Infrastructure;

public sealed class SharedSqliteDbFixtureIsolationTests
{
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
