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
		AddParameters(command, context.Variables);
		var affected = await command.ExecuteNonQueryAsync(cancellationToken);
		return new ConnectorExecutionResult(true, null, new Dictionary<string, object>(StringComparer.Ordinal) { ["affectedRows"] = affected });
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
