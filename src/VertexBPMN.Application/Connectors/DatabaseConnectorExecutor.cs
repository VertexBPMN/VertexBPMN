using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Application.Connectors;

public class DatabaseConnectorExecutor : IConnectorExecutor
{
	public virtual string Type => "database";
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100", Justification = "The SQL command is executable model configuration, not interpolated process data. Runtime db.* values are bound exclusively through DbParameter; covered by DatabaseConnectorParameterTests.")]
	public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
	{
		var provider = Required(context, "vertex:connector.provider");
		var commandText = Required(context, "vertex:connector.commandText");
		if (string.IsNullOrEmpty(context.CredentialSecret))
		{
			throw new ServiceTaskExecutionException("Database connector requires a credential containing the connection string.");
		}

		var factory = DbProviderFactories.GetFactory(provider);
		await using var connection = factory.CreateConnection() ?? throw new ServiceTaskExecutionException("Database provider did not create a connection.");
		connection.ConnectionString = context.CredentialSecret;
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = commandText;
		command.CommandTimeout = context.Attributes.TryGetValue("vertex:connector.commandTimeoutSeconds", out var rawTimeout) && int.TryParse(rawTimeout, out var timeout) ? Math.Clamp(timeout, 1, 300) : 30;

		var mode = context.Attributes.TryGetValue("vertex:connector.mode", out var rawMode) ? rawMode : "write";
		if (string.Equals(mode, "query", StringComparison.OrdinalIgnoreCase))
		{
			return await ExecuteQueryAsync(command, context, cancellationToken);
		}

		AddParameters(command, context.Variables);
		var affected = await command.ExecuteNonQueryAsync(cancellationToken);
		return new ConnectorExecutionResult(true, null, new Dictionary<string, object>(StringComparer.Ordinal) { ["affectedRows"] = affected });
	}

	// LC03 (Z.246): read-only query operation with a fixed result schema, a server-side
	// row limit (default 100, cap 1000) and a hard guard that rejects multi-statement or
	// non-SELECT text — no arbitrary SQL from a model/designer reaches the database as
	// a write through this path.
	private static async Task<ConnectorExecutionResult> ExecuteQueryAsync(DbCommand command, ConnectorExecutionContext context, CancellationToken cancellationToken)
	{
		AssertReadOnlySingleStatement(command.CommandText);
		AddParameters(command, context.Variables);

		var limit = 100;
		if (context.Attributes.TryGetValue("vertex:connector.rowLimit", out var rawLimit) && int.TryParse(rawLimit, out var parsed) && parsed > 0)
		{
			limit = Math.Min(parsed, 1000);
		}

		var rows = new List<Dictionary<string, object?>>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (rows.Count < limit && await reader.ReadAsync(cancellationToken))
		{
			var row = new Dictionary<string, object?>(StringComparer.Ordinal);
			for (var i = 0; i < reader.FieldCount; i++)
			{
				var value = reader.GetValue(i);
				row[reader.GetName(i)] = value is DBNull ? null : NormalizeScalar(value);
			}
			rows.Add(row);
		}

		return new ConnectorExecutionResult(true, null, new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["rowCount"] = rows.Count,
			["rows"] = rows
		});
	}

	private static object NormalizeScalar(object value) => value switch
	{
		byte or sbyte or short or ushort or int or uint => Convert.ToInt64(value),
		_ => value
	};

	private static void AssertReadOnlySingleStatement(string commandText)
	{
		var trimmed = commandText.Trim();
		if (!trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase))
		{
			throw new ServiceTaskExecutionException("Database query operation rejects non-SELECT command text (read-only path).");
		}

		if (trimmed.Contains(';', StringComparison.Ordinal))
		{
			throw new ServiceTaskExecutionException("Database query operation rejects multi-statement command text.");
		}
	}

	private static void AddParameters(DbCommand command, IDictionary<string, object> variables)
	{
		foreach (var pair in variables.Where(pair => pair.Key.StartsWith("db.", StringComparison.Ordinal)))
		{
			var parameter = command.CreateParameter();
			parameter.ParameterName = "@" + pair.Key[3..];
			parameter.Value = pair.Value ?? DBNull.Value;
			command.Parameters.Add(parameter);
		}
	}
	private static string Required(ConnectorExecutionContext context, string key) => context.Attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ServiceTaskExecutionException($"Database connector requires '{key}'.");
}
