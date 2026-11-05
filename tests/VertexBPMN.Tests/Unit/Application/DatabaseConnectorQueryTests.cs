using System.Data.Common;
using Microsoft.Data.Sqlite;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class DatabaseConnectorQueryTests : IDisposable
{
	private readonly string _providerName;
	private readonly string _connectionString;
	private readonly SqliteConnection _connection;

	public DatabaseConnectorQueryTests()
	{
		_providerName = $"vertex-test-{Guid.NewGuid():N}";
		DbProviderFactories.RegisterFactory(_providerName, SqliteFactory.Instance);
		_connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = $"connector-{Guid.NewGuid():N}",
			Mode = SqliteOpenMode.Memory,
			Cache = SqliteCacheMode.Shared
		}.ToString();
		_connection = new SqliteConnection(_connectionString);
		_connection.Open();
		using var setup = _connection.CreateCommand();
		setup.CommandText = "CREATE TABLE customers (id INTEGER NOT NULL, name TEXT NOT NULL)";
		setup.ExecuteNonQuery();
		using var seed = _connection.CreateCommand();
		seed.CommandText = "INSERT INTO customers (id, name) VALUES (1, 'A'), (2, 'B'), (3, 'C')";
		seed.ExecuteNonQuery();
	}

	[Fact]
	public async Task QueryMode_ReturnsRowsAndRowCountAsStructuredVariables()
	{
		var context = Context("query", "SELECT id, name FROM customers ORDER BY id");

		var result = await new DatabaseConnectorExecutor().ExecuteAsync(context, TestContext.Current.CancellationToken);

		Assert.True(result.Success);
		Assert.Equal(3, result.Outputs["rowCount"]);
		var rows = Assert.IsType<List<Dictionary<string, object?>>>(result.Outputs["rows"]);
		Assert.Equal(3, rows.Count);
		Assert.Equal("A", rows[0]["name"]);
		Assert.Equal(1L, rows[0]["id"]);
	}

	[Fact]
	public async Task QueryMode_EnforcesRowLimitCap()
	{
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["vertex:connector.provider"] = _providerName,
			["vertex:connector.commandText"] = "SELECT id, name FROM customers ORDER BY id",
			["vertex:connector.mode"] = "query",
			["vertex:connector.rowLimit"] = "2"
		};
		var context = new ConnectorExecutionContext("tenant-a", "sqlite", "query", null, attributes,
			new Dictionary<string, object>(StringComparer.Ordinal), new ConnectorRetryPolicy(1), CredentialSecret: _connectionString);

		var result = await new DatabaseConnectorExecutor().ExecuteAsync(context, TestContext.Current.CancellationToken);

		Assert.True(result.Success);
		Assert.Equal(2, result.Outputs["rowCount"]);
		Assert.Equal(2, Assert.IsType<List<Dictionary<string, object?>>>(result.Outputs["rows"]).Count);
	}

	[Theory]
	[InlineData("DELETE FROM customers")]
	[InlineData("SELECT id FROM customers; DROP TABLE customers")]
	public async Task QueryMode_RejectsNonReadOnlyOrMultiStatementSql(string sql)
	{
		var context = Context("query", sql);

		await Assert.ThrowsAsync<ServiceTaskExecutionException>(
			() => new DatabaseConnectorExecutor().ExecuteAsync(context, TestContext.Current.CancellationToken));
	}

	private ConnectorExecutionContext Context(string mode, string commandText)
	{
		return new ConnectorExecutionContext("tenant-a", "sqlite", mode, null,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["vertex:connector.provider"] = _providerName,
				["vertex:connector.commandText"] = commandText,
				["vertex:connector.mode"] = mode
			},
			new Dictionary<string, object>(StringComparer.Ordinal),
			new ConnectorRetryPolicy(1), CredentialSecret: _connectionString);
	}

	public void Dispose()
	{
		_connection.Dispose();
		GC.SuppressFinalize(this);
	}
}
