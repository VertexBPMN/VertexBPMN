using System.Data.Common;
using Microsoft.Data.Sqlite;
using VertexBPMN.Application.Connectors;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class DatabaseConnectorParameterTests
{
	[Fact]
	public async Task ProcessDataIsBoundAsAParameterAndCannotChangeTheConfiguredCommand()
	{
		var providerName = $"vertex-test-{Guid.NewGuid():N}";
		DbProviderFactories.RegisterFactory(providerName, SqliteFactory.Instance);
		try
		{
			var connectionString = new SqliteConnectionStringBuilder
			{
				DataSource = $"connector-{Guid.NewGuid():N}",
				Mode = SqliteOpenMode.Memory,
				Cache = SqliteCacheMode.Shared
			}.ToString();
			await using var connection = new SqliteConnection(connectionString);
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			await using var setup = connection.CreateCommand();
			setup.CommandText = "CREATE TABLE evidence (value TEXT NOT NULL)";
			await setup.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

			const string input = "'); DROP TABLE evidence; --";
			var context = new ConnectorExecutionContext("tenant-a", "sqlite", "insert", null,
				new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["vertex:connector.provider"] = providerName,
					["vertex:connector.commandText"] = "INSERT INTO evidence (value) VALUES (@value)"
				},
				new Dictionary<string, object>(StringComparer.Ordinal) { ["db.value"] = input },
				new ConnectorRetryPolicy(1), CredentialSecret: connectionString);

			var result = await new DatabaseConnectorExecutor().ExecuteAsync(context, TestContext.Current.CancellationToken);

			Assert.True(result.Success);
			Assert.Equal(1, result.Outputs["affectedRows"]);
			await using var query = connection.CreateCommand();
			query.CommandText = "SELECT value FROM evidence";
			Assert.Equal(input, await query.ExecuteScalarAsync(TestContext.Current.CancellationToken));
		}
		finally
		{
			DbProviderFactories.UnregisterFactory(providerName);
		}
	}
}
